namespace Amaigoma
{
   using MathNet.Numerics;
   using MathNet.Numerics.Distributions;
   using MathNet.Numerics.LinearAlgebra;
   using MathNet.Numerics.LinearAlgebra.Double;
   using MathNet.Numerics.Statistics;
   using Shouldly;
   using System;
   using System.Collections.Generic;
   using System.Collections.Immutable;
   using System.Linq;

   // Create a separate source file for this
   public static class IEnumerableExtensions
   {
      // Obtained from https://stackoverflow.com/a/1287572/263228
      public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Random rng)
      {
         T[] elements = source.ToArray();
         for (int i = elements.Length - 1; i >= 0; i--)
         {
            // Swap element "i" with a random earlier element it (or itself)
            // ... except we don't really need to swap it fully, as we can
            // return it immediately, and afterwards it's irrelevant.
            int swapIndex = rng.Next(i + 1);
            yield return elements[swapIndex];
            elements[swapIndex] = elements[i];
         }
      }
   }

   public class PakiraDecisionTreeGenerator
   {
      static public int UNKNOWN_CLASS_INDEX = -1;
      static public int INSUFFICIENT_SAMPLES_CLASS_INDEX = -2;
      static private int MINIMUM_SAMPLE_COUNT = 1000;
      static private double DEFAULT_CERTAINTY_SCORE = 0.95;
      private PassThroughTransformer DefaultDataTransformer = new PassThroughTransformer();
      private Random RandomSource = new Random();

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
         DiscreteUniform discreteUniform = new DiscreteUniform(0, 255, RandomSource);
         IList<double> trainSample = trainSamples.ElementAt(0);
         int featureCount = trainSample.Count();
         bool generateMoreData = true;
         int dataDistributionSamplesCount = MinimumSampleCount;
         ImmutableList<SabotenCache> trainSamplesCache = trainSamples.Select(d => new SabotenCache(d)).ToImmutableList();
         TanukiTransformers theTransformers = new TanukiTransformers(dataTransformers, trainSample);

         Matrix<double> dataDistributionSamples = Matrix<double>.Build.Dense(dataDistributionSamplesCount, featureCount, (i, j) => discreteUniform.Sample());
         ImmutableList<SabotenCache> dataDistributionSamplesCache = dataDistributionSamples.EnumerateRows().Select(d => new SabotenCache(d)).ToImmutableList();

         pakiraDecisionTreeModel.DataTransformers = theTransformers;

         while (generateMoreData)
         {
            generateMoreData = false;

            pakiraDecisionTreeModel.Tree = BuildTree(trainSamplesCache, trainLabels, dataDistributionSamplesCache, theTransformers);

            generateMoreData = pakiraDecisionTreeModel.Tree.GetNodes().Any(pakiraNode => (pakiraNode.IsLeaf && pakiraNode.Value == INSUFFICIENT_SAMPLES_CLASS_INDEX));

            List<IPakiraNode> insufficientSamplesNodes = pakiraDecisionTreeModel.Tree.GetNodes().FindAll(pakiraNode => (pakiraNode.IsLeaf && pakiraNode.Value == INSUFFICIENT_SAMPLES_CLASS_INDEX));

            DiscreteUniform discreteUniformBinary = new DiscreteUniform(0, 1, RandomSource);
            Vector<double> identity = Vector<double>.Build.Dense(featureCount, 1);

            foreach (IPakiraNode node in insufficientSamplesNodes)
            {
               IPakiraNode parent = pakiraDecisionTreeModel.Tree.GetParentNode(node);

               ImmutableList<SabotenCache> parentSamples = dataDistributionSamplesCache.Where(d => pakiraDecisionTreeModel.Tree.GetParentNode(pakiraDecisionTreeModel.PredictNode(d)) == parent).ToImmutableList();

               int parentSamplesCount = parentSamples.Count();
               int newValidSampleCount = 0;
               int invalidSampleCount = 0;

               while (newValidSampleCount < MinimumSampleCount)
               {
                  Vector<double> filter1 = Vector<double>.Build.Dense(featureCount, (i) => discreteUniformBinary.Sample());
                  Vector<double> filter2 = identity - filter1;

                  int firstSampleIndex = RandomSource.Next(parentSamplesCount);
                  int secondSampleIndex = RandomSource.Next(parentSamplesCount);

                  while (firstSampleIndex == secondSampleIndex)
                  {
                     secondSampleIndex = RandomSource.Next(parentSamplesCount);
                  }

                  SabotenCache firstSample = parentSamples[firstSampleIndex];
                  SabotenCache secondSample = parentSamples[secondSampleIndex];

                  DenseVector newData1 = DenseVector.OfEnumerable(firstSample.Data);
                  DenseVector newData2 = DenseVector.OfEnumerable(secondSample.Data);

                  newData1.PointwiseMultiply(filter1, newData1);
                  newData2.PointwiseMultiply(filter2, newData2);
                  newData1 += newData2;


                  SabotenCache newSample = new SabotenCache(newData1);

                  IPakiraNode predictedNode = pakiraDecisionTreeModel.PredictNode(newSample);
                  double predictedValue = predictedNode.Value;

                  if (predictedNode == node)
                  {
                     dataDistributionSamplesCache = dataDistributionSamplesCache.Add(newSample);
                     newValidSampleCount++;
                  }
                  else
                  {
                     invalidSampleCount++;
                  }
               }
            }

            dataDistributionSamplesCount *= 2;
         }

         pakiraDecisionTreeModel.DataDistributionSamples = dataDistributionSamples;
         pakiraDecisionTreeModel.DataDistributionSamplesCache = dataDistributionSamplesCache;
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
         ImmutableList<SabotenCache> extractedDataDistributionSamplesCacheList = extractedDataDistributionSamplesCache.ToImmutableList();
         IEnumerable<int> randomFeatureIndices =  Enumerable.Range(0, theTransformers.TotalOutputSamples).Shuffle(RandomSource);

         double bestScore = -1.0;
         int bestFeature = -1;

         foreach (int featureIndex in randomFeatureIndices)
         {
            double score = 0.0;

            extractedDataDistributionSamplesCacheList = extractedDataDistributionSamplesCacheList.Prefetch(featureIndex, theTransformers).ToImmutableList();

            ImmutableList<double> featureDataDistributionSample = extractedDataDistributionSamplesCacheList.Select<SabotenCache, double>(sample =>
            {
               return sample[featureIndex];
            }
            ).ToImmutableList();

            double count = featureDataDistributionSample.Count();

            Histogram histogram = new Histogram(featureDataDistributionSample, 10);

            // LowerBound always has an offset
            histogram.LowerBound.ShouldBeGreaterThanOrEqualTo((0.0).Decrement());
            histogram.UpperBound.ShouldBeLessThanOrEqualTo(255.0);

            extractedTrainSamplesCache = extractedTrainSamplesCache.Prefetch(featureIndex, theTransformers).ToImmutableList();

            score = extractedTrainSamplesCache.Max((SabotenCache trainSample) =>
            {
               double trainSampleValue = trainSample[featureIndex];

               trainSampleValue.ShouldBeGreaterThanOrEqualTo(0.0);
               trainSampleValue.ShouldBeLessThanOrEqualTo(255.0);

               // LowerBound is not included in bucket
               if ((trainSampleValue > histogram.LowerBound) && (trainSampleValue <= histogram.UpperBound))
               {
                  return count - histogram.GetBucketOf(trainSampleValue).Count;
               }
               else
               {
                  // We might also end up here when the training sample falls between
                  // the last tree split and the min/max of the distribution samples.
                  // In that case it is not a very good split but this should happen
                  // very rarely and it is not worse than a random split.
                  return count;
               }
            }
            );

            score /= count;

            if(score > bestScore)
            {
               bestScore = score;
               bestFeature = featureIndex;
            }

            if (score >= CertaintyScore)
            {
               break;
            }
         }

         bestFeature.ShouldBeGreaterThanOrEqualTo(0);

         double bestFeatureAverage = extractedDataDistributionSamplesCacheList.Select(sample => sample[bestFeature]).Mean();

         return new Tuple<int, double, double, IEnumerable<SabotenCache>, IEnumerable<SabotenCache>>(bestFeature, bestScore, bestFeatureAverage, extractedDataDistributionSamplesCacheList, extractedTrainSamplesCache);
      }
   }
}