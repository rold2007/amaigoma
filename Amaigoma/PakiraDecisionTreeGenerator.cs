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
      static public readonly int UNKNOWN_CLASS_INDEX = -1;
      static public readonly int INSUFFICIENT_SAMPLES_CLASS_INDEX = -2;
      static public readonly int randomSeed = new Random().Next();
      static private readonly int MINIMUM_SAMPLE_COUNT = 1000;
      static private readonly double DEFAULT_CERTAINTY_SCORE = 0.95;
      static private readonly PassThroughTransformer DefaultDataTransformer = new PassThroughTransformer();
      private readonly Random RandomSource = new Random(randomSeed);
      private DiscreteUniform discreteUniform;

      public PakiraDecisionTreeGenerator()
      {
         MinimumSampleCount = MINIMUM_SAMPLE_COUNT;
         CertaintyScore = DEFAULT_CERTAINTY_SCORE;
         discreteUniform = new DiscreteUniform(0, 255, RandomSource);
      }

      public int MinimumSampleCount { get; set; }

      public double CertaintyScore { get; set; }

      public PakiraDecisionTreeModel Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<IList<double>> trainSamples, IList<double> trainLabels)
      {
         IList<double> trainSample = trainSamples.ElementAt(0);
         int featureCount = trainSample.Count();
         bool generateMoreData = true;
         int dataDistributionSamplesCount = MinimumSampleCount;
         ImmutableList<SabotenCache> trainSamplesCache = trainSamples.Select(d => new SabotenCache(d)).ToImmutableList();
         ImmutableList<double> immutableTrainLabels = trainLabels.ToImmutableList();

         ImmutableList<SabotenCache> dataDistributionSamplesCache;

         {
            Matrix<double> dataDistributionSamples = Matrix<double>.Build.Dense(dataDistributionSamplesCount, featureCount, (i, j) => discreteUniform.Sample());
            dataDistributionSamplesCache = dataDistributionSamples.EnumerateRows().Select(d => new SabotenCache(d)).ToImmutableList();
         }

         while (generateMoreData)
         {
            generateMoreData = false;

            pakiraDecisionTreeModel = BuildTree(trainSamplesCache, immutableTrainLabels, dataDistributionSamplesCache, pakiraDecisionTreeModel);

            generateMoreData = pakiraDecisionTreeModel.Tree.GetNodes().Any(pakiraNode => (pakiraNode.IsLeaf && pakiraNode.Value == INSUFFICIENT_SAMPLES_CLASS_INDEX));

            List<IPakiraNode> insufficientSamplesNodes = pakiraDecisionTreeModel.Tree.GetNodes().FindAll(pakiraNode => (pakiraNode.IsLeaf && pakiraNode.Value == INSUFFICIENT_SAMPLES_CLASS_INDEX));

            DiscreteUniform discreteUniformBinary = new DiscreteUniform(0, 1, RandomSource);
            Vector<double> identity = Vector<double>.Build.Dense(featureCount, 1);

            foreach (IPakiraNode node in insufficientSamplesNodes)
            {
               IPakiraNode parent = pakiraDecisionTreeModel.Tree.GetParentNode(node);

               ImmutableList<SabotenCache> parentSamples = dataDistributionSamplesCache.Where(d => pakiraDecisionTreeModel.Tree.GetParentNode(pakiraDecisionTreeModel.PredictNode(d)) == parent).ToImmutableList();

               int parentSamplesCount = parentSamples.Count();

               const int sampleSize = 100;
               const int minimumValidSampleCount = (int)(0.20 * sampleSize);
               int newValidSampleCount = 0;
               int invalidSampleCount = 0;
               double randomProportion = 100.0;

               while (newValidSampleCount < MinimumSampleCount)
               {
                  for (int i = 0; i < sampleSize; i++)
                  {
                     SabotenCache newSampleCache;

                     if (randomProportion < 100.0)
                     {
                        int dataSampleIndex = RandomSource.Next(parentSamplesCount);
                        SabotenCache dataSample = parentSamples[dataSampleIndex];

                        newSampleCache = new SabotenCache(DenseVector.Create(featureCount, (i) =>
                        {
                           if (RandomSource.NextDouble() * 100 < randomProportion)
                           {
                              return discreteUniform.Sample();
                           }
                           else
                           {
                              dataSample = pakiraDecisionTreeModel.Prefetch(dataSample, i);

                              return dataSample[i];
                           }
                        }
                        ));
                     }
                     else
                     {
                        newSampleCache = new SabotenCache(DenseVector.Create(featureCount, (i) => discreteUniform.Sample()));
                     }

                     IPakiraNode predictedNode = pakiraDecisionTreeModel.PredictNode(newSampleCache);
                     double predictedValue = predictedNode.Value;

                     if (predictedNode == node)
                     {
                        dataDistributionSamplesCache = dataDistributionSamplesCache.Add(newSampleCache);
                        newValidSampleCount++;
                     }
                     else
                     {
                        invalidSampleCount++;
                     }
                  }

                  if (newValidSampleCount < minimumValidSampleCount)
                  {
                     randomProportion -= 10;

                     // Don't let fully-unchanged new samples
                     randomProportion = Math.Max(randomProportion, 10);
                  }

                  newValidSampleCount.ShouldNotBe(-1, "Just to put a breakpoint and see the values...");
               }
            }
         }

         return pakiraDecisionTreeModel.UpdateDataDistributionSamplesCache(dataDistributionSamplesCache);
      }

      static private bool ThresholdCompareLessThanOrEqual(double inputValue, double threshold)
      {
         return inputValue <= threshold;
      }

      private struct ProcessNode
      {
         public ProcessNode(IPakiraNode node, ImmutableList<SabotenCache> trainSamplesCache, ImmutableList<double> trainLabels, ImmutableList<SabotenCache> dataDistributionSamplesCache)
         {
            Node = node;
            TrainSamplesCache = trainSamplesCache;
            TrainLabels = trainLabels;
            DataDistributionSamplesCache = dataDistributionSamplesCache;
         }

         public IPakiraNode Node;
         public ImmutableList<SabotenCache> TrainSamplesCache;
         public ImmutableList<double> TrainLabels;
         public ImmutableList<SabotenCache> DataDistributionSamplesCache;
      };

      private PakiraDecisionTreeModel BuildTree(ImmutableList<SabotenCache> trainSamplesCache, ImmutableList<double> trainLabels, ImmutableList<SabotenCache> dataDistributionSamplesCache, PakiraDecisionTreeModel pakiraDecisionTreeModel)
      {
         int distinctCount = trainLabels.Distinct().Take(2).Count();

         distinctCount.ShouldBeGreaterThanOrEqualTo(1);

         if (distinctCount == 1)
         {
            return pakiraDecisionTreeModel.UpdateTree(PakiraTree.Empty);
         }

         PakiraLeaf[] leaves = new PakiraLeaf[2];
         PakiraTree pakiraTree = PakiraTree.Empty.AddLeaf(new PakiraLeaf(UNKNOWN_CLASS_INDEX));
         Stack<ProcessNode> processNodes = new Stack<ProcessNode>();

         processNodes.Push(new ProcessNode(pakiraTree.Root, trainSamplesCache, trainLabels, dataDistributionSamplesCache));

         while (processNodes.Count > 0)
         {
            ProcessNode processNode = processNodes.Pop();

            ImmutableList<SabotenCache> processNodeDataDistributionSamplesCache = processNode.DataDistributionSamplesCache;
            ImmutableList<SabotenCache> extractedDataDistributionSamplesCache = processNodeDataDistributionSamplesCache.Take(MinimumSampleCount).ToImmutableList();
            ImmutableList<SabotenCache> processNodeTrainSamplesCache = processNode.TrainSamplesCache;
            ImmutableList<double> processNodeTrainLabels = processNode.TrainLabels;

            int extractedDataDistributionSamplesCount = extractedDataDistributionSamplesCache.Count();

            if (extractedDataDistributionSamplesCount >= MinimumSampleCount)
            {
               Tuple<int, double, ImmutableList<SabotenCache>, ImmutableList<SabotenCache>> tuple = GetBestSplit(extractedDataDistributionSamplesCache, processNodeTrainSamplesCache, pakiraDecisionTreeModel);
               int bestFeatureIndex = tuple.Item1;
               double threshold = tuple.Item2;
               ImmutableList<SabotenCache> bestSplitDataDistributionSamplesCache = tuple.Item3;
               ImmutableList<SabotenCache> bestSplitTrainSamplesCache = tuple.Item4;

               ImmutableList<SabotenCache> concatenatedDataDistributionSamples = bestSplitDataDistributionSamplesCache.Concat(processNodeDataDistributionSamplesCache.Skip(MinimumSampleCount)).ToImmutableList();

               PakiraNode node = new PakiraNode(bestFeatureIndex, threshold);

               concatenatedDataDistributionSamples = pakiraDecisionTreeModel.Prefetch(concatenatedDataDistributionSamples, bestFeatureIndex);

               for (int leafIndex = 0; leafIndex < 2; leafIndex++)
               {
                  bool theKey = (leafIndex == 0);

                  ImmutableList<SabotenCache> slice = bestSplitTrainSamplesCache.Where(column => ThresholdCompareLessThanOrEqual(column[bestFeatureIndex], threshold) == theKey).ToImmutableList();

                  if (slice.Count() > 0)
                  {
                     ImmutableList<double> ySlice = processNodeTrainLabels.Where(
                     (trainLabel, trainLabelIndex) =>
                     {
                        double trainSample = bestSplitTrainSamplesCache.ElementAt(trainLabelIndex)[bestFeatureIndex];

                        return ThresholdCompareLessThanOrEqual(trainSample, threshold) == theKey;
                     }
                     ).ToImmutableList();

                     int distinctLabelsCount = ySlice.Distinct().Count();

                     // only one answer, set leaf
                     if (distinctLabelsCount == 1)
                     {
                        double leafValue = ySlice.First();

                        leaves[leafIndex] = new PakiraLeaf(leafValue);
                     }
                     // otherwise continue to build tree
                     else
                     {
                        leaves[leafIndex] = new PakiraLeaf(UNKNOWN_CLASS_INDEX);

                        concatenatedDataDistributionSamples.Count().ShouldBeGreaterThan(0);

                        ImmutableList<SabotenCache> sampleSliceCache = concatenatedDataDistributionSamples.Where(column => ThresholdCompareLessThanOrEqual(column[bestFeatureIndex], threshold) == theKey).ToImmutableList();

                        sampleSliceCache.Count().ShouldNotBe(concatenatedDataDistributionSamples.Count());

                        processNodes.Push(new ProcessNode(leaves[leafIndex], slice, ySlice, sampleSliceCache));
                     }
                  }
                  else
                  {
                     // We don't have any training data for this node
                     leaves[leafIndex] = new PakiraLeaf(UNKNOWN_CLASS_INDEX);
                  }
               }

               pakiraTree = pakiraTree.ReplaceLeaf(processNode.Node as PakiraLeaf, PakiraTree.Empty.AddNode(node, leaves[0], leaves[1]));
            }
            else
            {
               pakiraTree = pakiraTree.ReplaceLeaf(processNode.Node as PakiraLeaf, PakiraTree.Empty.AddLeaf(new PakiraLeaf(INSUFFICIENT_SAMPLES_CLASS_INDEX)));
            }
         }

         return pakiraDecisionTreeModel.UpdateTree(pakiraTree);
      }

      private Tuple<int, double, ImmutableList<SabotenCache>, ImmutableList<SabotenCache>> GetBestSplit(ImmutableList<SabotenCache> extractedDataDistributionSamplesCache, ImmutableList<SabotenCache> extractedTrainSamplesCache, PakiraDecisionTreeModel pakiraDecisionTreeModel)
      {
         ImmutableList<SabotenCache> extractedDataDistributionSamplesCacheList = extractedDataDistributionSamplesCache.ToImmutableList();
         ImmutableList<int> randomFeatureIndices = pakiraDecisionTreeModel.FeatureIndices().Shuffle(RandomSource).ToImmutableList();

         double bestScore = -1.0;
         int bestFeature = -1;

         foreach (int featureIndex in randomFeatureIndices)
         {
            double score = 0.0;

            extractedDataDistributionSamplesCacheList = pakiraDecisionTreeModel.Prefetch(extractedDataDistributionSamplesCacheList, featureIndex);

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

            extractedTrainSamplesCache = pakiraDecisionTreeModel.Prefetch(extractedTrainSamplesCache, featureIndex);

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

            if (score > bestScore)
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

         return new Tuple<int, double, ImmutableList<SabotenCache>, ImmutableList<SabotenCache>>(bestFeature, bestFeatureAverage, extractedDataDistributionSamplesCacheList, extractedTrainSamplesCache);
      }
   }
}