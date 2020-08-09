namespace Amaigoma
{
   using MathNet.Numerics.Distributions;
   using MathNet.Numerics.LinearAlgebra;
   using MathNet.Numerics.Statistics;
   using Shouldly;
   using System;
   using System.Collections.Generic;
   using System.Collections.Immutable;
   using System.Linq;

   public class PakiraDecisionTreeGenerator
   {
      static public int UNKNOWN_CLASS_INDEX = -1;
      static public int INSUFFICIENT_SAMPLES_CLASS_INDEX = -2;
      static private int MINIMUM_SAMPLE_COUNT = 1000;
      static private double DEFAULT_CERTAINTY_SCORE = 0.95;
      private PassThroughTransformer DefaultDataTransformer = new PassThroughTransformer();

      public PakiraDecisionTreeGenerator()
      {
         MinimumSampleCount = MINIMUM_SAMPLE_COUNT;
         CertaintyScore = DEFAULT_CERTAINTY_SCORE;
      }

      public int MinimumSampleCount { get; set; }

      public double CertaintyScore { get; set; }

      public void Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<IList<double>> trainSamples, IList<double> trainLabels)
      {
         Generate(pakiraDecisionTreeModel, trainSamples, trainLabels, DefaultDataTransformer.ConvertAll);
      }

      public void Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<IList<double>> trainSamples, IList<double> trainLabels, Converter<IList<double>, IList<double>> dataTransformers)
      {
         ContinuousUniform continuousUniform = new ContinuousUniform(0, 256);
         IList<double> trainSample = trainSamples.ElementAt(0);
         int featureCount = trainSample.Count();
         bool generateMoreData = true;
         int dataDistributionSamplesCount = MinimumSampleCount * 3;
         ImmutableList<SabotenCache> trainSamplesCache = trainSamples.Select(d => new SabotenCache(d)).ToImmutableList();
         TanukiTransformers theTransformers = new TanukiTransformers(dataTransformers, trainSample);

         Matrix<double> dataDistributionSamples = Matrix<double>.Build.Dense(dataDistributionSamplesCount, featureCount, (i, j) => continuousUniform.Sample());
         ImmutableList<SabotenCache> dataDistributionSamplesCache = dataDistributionSamples.EnumerateRows().Select(d => new SabotenCache(d)).ToImmutableList();

         while (generateMoreData)
         {
            generateMoreData = false;

            if(dataDistributionSamplesCache.Count() < dataDistributionSamplesCount)
            {
               dataDistributionSamples = Matrix<double>.Build.Dense(dataDistributionSamplesCount - dataDistributionSamplesCache.Count(), featureCount, (i, j) => continuousUniform.Sample());

               dataDistributionSamplesCache = dataDistributionSamplesCache.AddRange(dataDistributionSamples.EnumerateRows().Select(d => new SabotenCache(d)));
            }

            pakiraDecisionTreeModel.Tree = BuildTree(trainSamplesCache, trainLabels, dataDistributionSamplesCache, theTransformers);

            generateMoreData = pakiraDecisionTreeModel.Tree.GetNodes().Any(pakiraNode => (pakiraNode.IsLeaf && pakiraNode.Value == INSUFFICIENT_SAMPLES_CLASS_INDEX));

            dataDistributionSamplesCount *= 2;
         }

         pakiraDecisionTreeModel.DataTransformers = theTransformers;
      }

      static private bool ThresholdCompareLessThanOrEqual(double inputValue, double threshold)
      {
         return inputValue <= threshold;
      }

      static private bool ThresholdCompareGreater(double inputValue, double threshold)
      {
         return inputValue > threshold;
      }

      private PakiraTree BuildTree(ImmutableList<SabotenCache> trainSamplesCache, IEnumerable<double> trainLabels, IEnumerable<SabotenCache> dataDistributionSamplesCache, TanukiTransformers theTransformers)
      {
         IEnumerable<SabotenCache> extractedDataDistributionSamplesCache = dataDistributionSamplesCache.Take(MinimumSampleCount);

         int extractedDataDistributionSamplesCount = extractedDataDistributionSamplesCache.Count();

         if (extractedDataDistributionSamplesCount < MinimumSampleCount)
         {
            return PakiraTree.Empty.AddLeaf(new PakiraLeaf(INSUFFICIENT_SAMPLES_CLASS_INDEX));
         }

         Tuple<int, double, double, IEnumerable<SabotenCache>, IEnumerable<SabotenCache>> tuple = GetBestSplit(extractedDataDistributionSamplesCache, trainSamplesCache, theTransformers);
         int bestFeatureIndex = tuple.Item1;
         double gain = tuple.Item2;
         double threshold = tuple.Item3;
         IEnumerable<SabotenCache> bestSplitDataDistributionSamplesCache = tuple.Item4;
         IEnumerable<SabotenCache> bestSplitTrainSamplesCache = tuple.Item5;

         ImmutableList<SabotenCache> concatenatedDataDistributionSamples = bestSplitDataDistributionSamplesCache.Concat(dataDistributionSamplesCache.Skip(MinimumSampleCount)).ToImmutableList();

         Func<double, double, bool>[] compareFunctions = { ThresholdCompareLessThanOrEqual, ThresholdCompareGreater };

         PakiraTree[] children = new PakiraTree[2];

         for (int i = 0; i < compareFunctions.Length; i++)
         {
            concatenatedDataDistributionSamples = concatenatedDataDistributionSamples.Prefetch(bestFeatureIndex, theTransformers).ToImmutableList();

            IEnumerable<SabotenCache> sampleSliceCache = concatenatedDataDistributionSamples.Where(column => compareFunctions[i](column[bestFeatureIndex], threshold));

            ImmutableList<SabotenCache> slice = bestSplitTrainSamplesCache.Where(column => compareFunctions[i](column[bestFeatureIndex], threshold)).ToImmutableList();
            PakiraTree child;

            if (slice.Count() > 0)
            {
               IEnumerable<double> ySlice = trainLabels.Where(
               (trainLabel, trainLabelIndex) =>
               {
                  double trainSample = bestSplitTrainSamplesCache.ElementAt(trainLabelIndex)[bestFeatureIndex];

                  return compareFunctions[i](trainSample, threshold);
               }
               );

               int labelCount = ySlice.Distinct().Count();

               // only one answer, set leaf
               if (labelCount == 1)
               {
                  child = PakiraTree.Empty.AddLeaf(new PakiraLeaf(ySlice.First()));
               }
               // otherwise continue to build tree
               else
               {
                  child = BuildTree(slice, ySlice, sampleSliceCache, theTransformers);
               }
            }
            else
            {
               // We don't have any training data for this node
               child = PakiraTree.Empty.AddLeaf(new PakiraLeaf(UNKNOWN_CLASS_INDEX));
            }

            children[i] = child;
         }

         PakiraNode node = new PakiraNode(bestFeatureIndex, threshold);
         PakiraTree tree = PakiraTree.Empty.AddNode(node, children[0], children[1]);

         return tree;
      }

      private Tuple<int, double, double, IEnumerable<SabotenCache>, IEnumerable<SabotenCache>> GetBestSplit(IEnumerable<SabotenCache> extractedDataDistributionSamplesCache, ImmutableList<SabotenCache> extractedTrainSamplesCache, TanukiTransformers theTransformers)
      {
         double[] scores = new double[theTransformers.TotalOutputSamples];
         ImmutableList<SabotenCache> extractedDataDistributionSamplesCacheList = extractedDataDistributionSamplesCache.ToImmutableList();
         int evaluatedScoreIndex = 0;
         bool continueScoreEvaluation = evaluatedScoreIndex < theTransformers.TotalOutputSamples;

         while (continueScoreEvaluation)
         {
            double score = 0.0;

            extractedDataDistributionSamplesCacheList = extractedDataDistributionSamplesCacheList.Prefetch(evaluatedScoreIndex, theTransformers).ToImmutableList();

            ImmutableList<double> featureDataDistributionSample = extractedDataDistributionSamplesCacheList.Select<SabotenCache, double>(sample =>
            {
               return sample[evaluatedScoreIndex];
            }
            ).ToImmutableList();

            extractedTrainSamplesCache = extractedTrainSamplesCache.Prefetch(evaluatedScoreIndex, theTransformers).ToImmutableList();

            foreach (SabotenCache trainSample in extractedTrainSamplesCache)
            {
               double trainSampleValue = trainSample[evaluatedScoreIndex];
               double quantileRank = featureDataDistributionSample.QuantileRank(trainSampleValue, RankDefinition.Default);

               // Keep the sample farthest in the data distribution
               score = Math.Max(score, Math.Max(quantileRank, 1.0 - quantileRank));
            }

            scores[evaluatedScoreIndex] = score;

            evaluatedScoreIndex++;

            if (score >= CertaintyScore)
            {
               continueScoreEvaluation = false;
            }
            else 
            {
               continueScoreEvaluation = evaluatedScoreIndex < theTransformers.TotalOutputSamples;
            }
         }

         double bestGain = 0.0;
         int bestFeature = -1;

         for (int featureIndex = 0; featureIndex < evaluatedScoreIndex; featureIndex++)
         {
            double gain = scores[featureIndex];

            if (gain > bestGain)
            {
               bestGain = gain;
               bestFeature = featureIndex;
            }
         }

         bestFeature.ShouldBeGreaterThanOrEqualTo(0);

         double bestFeatureAverage = extractedDataDistributionSamplesCacheList.Select(sample => sample[bestFeature]).Mean();

         return new Tuple<int, double, double, IEnumerable<SabotenCache>, IEnumerable<SabotenCache>>(bestFeature, bestGain, bestFeatureAverage, extractedDataDistributionSamplesCacheList, extractedTrainSamplesCache);
      }
   }
}