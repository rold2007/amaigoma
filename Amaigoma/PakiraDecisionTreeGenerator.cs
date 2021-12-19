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

   public sealed record TrainData
   {
      public ImmutableList<ImmutableList<double>> Samples { get; } = ImmutableList<ImmutableList<double>>.Empty;
      public ImmutableList<double> Labels { get; } = ImmutableList<double>.Empty;

      public TrainData()
      {
      }

      public TrainData(ImmutableList<ImmutableList<double>> samples, ImmutableList<double> labels)
      {
         Samples = samples;
         Labels = labels;
      }

      public TrainData AddSample(IEnumerable<double> sample, double label)
      {
         ImmutableList<double> immutableSample = sample.ToImmutableList();

         if (!Samples.IsEmpty)
         {
            immutableSample.Count.ShouldBe(Samples[0].Count);
         }

         return new TrainData(Samples.Add(immutableSample), Labels.Add(label));
      }
   }

   public sealed record TrainDataCache
   {
      public ImmutableList<SabotenCache> Samples { get; } = ImmutableList<SabotenCache>.Empty;
      public ImmutableList<double> Labels { get; } = ImmutableList<double>.Empty;

      public TrainDataCache(ImmutableList<SabotenCache> samples, ImmutableList<double> labels)
      {
         Samples = samples;
         Labels = labels;
      }

      public TrainDataCache AddSamples(TrainDataCache samples)
      {
         samples.Samples.Count.ShouldBeGreaterThan(0);
         samples.Labels.Count.ShouldBeGreaterThan(0);

         if (!Samples.IsEmpty)
         {
            samples.Samples[0].Data.Count.ShouldBe(Samples[0].Data.Count);
         }

         return new TrainDataCache(Samples.AddRange(samples.Samples), Labels.AddRange(samples.Labels));
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
      private readonly DiscreteUniform discreteUniform;

      public PakiraDecisionTreeGenerator()
      {
         MinimumSampleCount = MINIMUM_SAMPLE_COUNT;
         CertaintyScore = DEFAULT_CERTAINTY_SCORE;
         discreteUniform = new DiscreteUniform(0, 255, RandomSource);
      }

      public int MinimumSampleCount { get; set; }

      public double CertaintyScore { get; set; }

      public PakiraDecisionTreeModel Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, TrainData trainData)
      {
         ImmutableList<double> trainSample = trainData.Samples[0];
         int featureCount = trainSample.Count();
         bool generateMoreData = true;
         ImmutableList<SabotenCache> trainSamplesCache = trainData.Samples.Select(d => new SabotenCache(d)).ToImmutableList();
         ImmutableList<double> immutableTrainLabels = trainData.Labels;
         bool updateFeatureMasks = (featureCount > 1);

         if (pakiraDecisionTreeModel.Tree.Root == null)
         {
            PakiraLeaf initialLeaf = new PakiraLeaf(INSUFFICIENT_SAMPLES_CLASS_INDEX);

            pakiraDecisionTreeModel = pakiraDecisionTreeModel.UpdateTree(PakiraTree.Empty.AddLeaf(initialLeaf));
            pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataDistributionSamplesCache(initialLeaf, ImmutableList<SabotenCache>.Empty);
         }

         for (int trainSampleIndex = 0; trainSampleIndex < trainSamplesCache.Count; trainSampleIndex++)
         {
            PakiraDecisionTreePredictionResult pakiraDecisionTreePredictionResult = pakiraDecisionTreeModel.PredictNode(trainSamplesCache[trainSampleIndex]);

            pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(pakiraDecisionTreePredictionResult.PakiraLeaf, new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(pakiraDecisionTreePredictionResult.SabotenCache), ImmutableList<double>.Empty.Add(immutableTrainLabels[trainSampleIndex])));
         }

         ImmutableDictionary<int, Vector<double>> randomFeaturesMasks = ImmutableDictionary<int, Vector<double>>.Empty;
         ImmutableList<int> randomFeatureIndices = Enumerable.Range(0, featureCount).OrderBy(x => RandomSource.Next()).ToImmutableList();
         Vector<double> updatedFeaturesMask = DenseVector.Build.Dense(featureCount);

         for (int featureIndex = 1; featureIndex < featureCount; featureIndex++)
         {
            Vector<double> featuresMask = DenseVector.Build.Dense(featureCount);

            foreach (int randomFeatureIndex in randomFeatureIndices.Take(featureIndex))
            {
               featuresMask[randomFeatureIndex] = 1;
            };

            randomFeaturesMasks = randomFeaturesMasks.Add(featureIndex, featuresMask);
         }

         randomFeaturesMasks = randomFeaturesMasks.Add(featureCount, DenseVector.Build.Dense(featureCount, 1));

         while (generateMoreData)
         {
            pakiraDecisionTreeModel = BuildTree(pakiraDecisionTreeModel);

            List<PakiraLeaf> insufficientSamplesLeaves = pakiraDecisionTreeModel.Tree.GetNodes().FindAll(pakiraNode => pakiraNode.IsLeaf && pakiraNode.Value == INSUFFICIENT_SAMPLES_CLASS_INDEX).Cast<PakiraLeaf>().ToList();

            foreach (PakiraLeaf leaf in insufficientSamplesLeaves)
            {
               ImmutableList<SabotenCache> dataDistributionSamplesCache = pakiraDecisionTreeModel.DataDistributionSamplesCache(leaf);
               ImmutableList<SabotenCache> newSamplesCache = ImmutableList<SabotenCache>.Empty;
               int samplesCount = dataDistributionSamplesCache.Count();
               int totalNewValidSampleCount = 0;
               int randomFeaturesCount = featureCount;
               int sampleSize = 10;
               int minimumValidSampleCount = Math.Max(1, (int)(0.20 * sampleSize));
               int newSampleCountNeeded = MinimumSampleCount - dataDistributionSamplesCache.Count;

               while (totalNewValidSampleCount < newSampleCountNeeded)
               {
                  int newValidSampleCount = 0;

                  randomFeaturesCount.ShouldBeGreaterThan(0);
                  randomFeaturesCount.ShouldBeLessThanOrEqualTo(featureCount);

                  ImmutableList<SabotenCache> generatedSamplesCache = ImmutableList<SabotenCache>.Empty;

                  Vector<double> featuresMask = randomFeaturesMasks[randomFeaturesCount];

                  for (int i = 0; i < sampleSize; i++)
                  {
                     generatedSamplesCache = generatedSamplesCache.Add(new SabotenCache(DenseVector.Create(featureCount, (dataIndex) =>
                     {
                        return (featuresMask[dataIndex] == 1.0) ? discreteUniform.Sample() : dataDistributionSamplesCache[RandomSource.Next(samplesCount)].Data[dataIndex];
                     }
                     )));

                     // Mix the feature mask to generate different patterns
                     if (updateFeatureMasks)
                     {
                        int featureMasksSplitIndex = RandomSource.Next(1, featureCount);
                        int firstCopySize = featureCount - featureMasksSplitIndex;
                        int secondCopySize = featureCount - firstCopySize;

                        featuresMask.CopySubVectorTo(updatedFeaturesMask, featureMasksSplitIndex, 0, firstCopySize);
                        featuresMask.CopySubVectorTo(updatedFeaturesMask, 0, firstCopySize, secondCopySize);

                        updatedFeaturesMask.CopyTo(featuresMask);
                     }
                  }

                  foreach (SabotenCache newSampleCache in generatedSamplesCache)
                  {
                     PakiraDecisionTreePredictionResult pakiraDecisionTreePredictionResult = pakiraDecisionTreeModel.PredictNode(newSampleCache);

                     if (pakiraDecisionTreePredictionResult.PakiraLeaf == leaf)
                     {
                        newSamplesCache = newSamplesCache.Add(pakiraDecisionTreePredictionResult.SabotenCache);
                        newValidSampleCount++;
                     }
                     else
                     {
                        // Prevent keeping too many samples and explode the memory usage
                        if (pakiraDecisionTreePredictionResult.PakiraLeaf.Value == INSUFFICIENT_SAMPLES_CLASS_INDEX)
                        {
                           if (pakiraDecisionTreeModel.DataDistributionSamplesCache(pakiraDecisionTreePredictionResult.PakiraLeaf).Count < MinimumSampleCount)
                           {
                              pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataDistributionSamplesCache(pakiraDecisionTreePredictionResult.PakiraLeaf, ImmutableList<SabotenCache>.Empty.Add(pakiraDecisionTreePredictionResult.SabotenCache));
                           }
                        }
                     }
                  }

                  if (newValidSampleCount < minimumValidSampleCount)
                  {
                     if (randomFeaturesCount > 1)
                     {
                        randomFeaturesCount = Math.Max(1, randomFeaturesCount /= 2);
                     }
                     else
                     {
                        sampleSize = Math.Min(sampleSize * 2, (MinimumSampleCount - totalNewValidSampleCount) * 2);
                        minimumValidSampleCount = 0;
                     }
                  }
                  else
                  {
                     sampleSize = Math.Min(sampleSize * 2, (MinimumSampleCount - totalNewValidSampleCount) * 2);
                     minimumValidSampleCount = Math.Max(1, (int)(0.20 * sampleSize));
                  }

                  totalNewValidSampleCount += newValidSampleCount;
               }

               pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataDistributionSamplesCache(leaf, newSamplesCache);
            }

            generateMoreData = insufficientSamplesLeaves.Count() > 0;
         }

         return pakiraDecisionTreeModel;
      }

      static private bool ThresholdCompareLessThanOrEqual(double inputValue, double threshold)
      {
         return inputValue <= threshold;
      }

      private struct ProcessNode
      {
         public ProcessNode(IPakiraNode node, TrainDataCache trainSamplesCache, ImmutableList<SabotenCache> dataDistributionSamplesCache)
         {
            Node = node;
            TrainSamplesCache = trainSamplesCache;
            DataDistributionSamplesCache = dataDistributionSamplesCache;
         }

         public IPakiraNode Node;
         public TrainDataCache TrainSamplesCache;
         public ImmutableList<SabotenCache> DataDistributionSamplesCache;
      };

      private PakiraDecisionTreeModel BuildTree(PakiraDecisionTreeModel pakiraDecisionTreeModel)
      {
         Stack<ProcessNode> processNodes = new Stack<ProcessNode>();
         PakiraTree pakiraTree = pakiraDecisionTreeModel.Tree;

         {
            List<PakiraLeaf> insufficientSamplesLeaves = pakiraDecisionTreeModel.Tree.GetNodes().FindAll(pakiraNode => pakiraNode.IsLeaf && pakiraNode.Value == INSUFFICIENT_SAMPLES_CLASS_INDEX).Cast<PakiraLeaf>().ToList();

            foreach (PakiraLeaf pakiraLeaf in insufficientSamplesLeaves)
            {
               processNodes.Push(new ProcessNode(pakiraLeaf, pakiraDecisionTreeModel.TrainDataCache(pakiraLeaf), pakiraDecisionTreeModel.DataDistributionSamplesCache(pakiraLeaf)));
            }
         }

         PakiraLeaf[] leaves = new PakiraLeaf[2];
         ImmutableList<SabotenCache>[] sampleSliceCache = new ImmutableList<SabotenCache>[2];
         ImmutableList<SabotenCache>[] slice = new ImmutableList<SabotenCache>[2];
         ImmutableList<double>[] ySlice = new ImmutableList<double>[2];

         while (processNodes.Count > 0)
         {
            ProcessNode processNode = processNodes.Pop();

            ImmutableList<SabotenCache> processNodeDataDistributionSamplesCache = processNode.DataDistributionSamplesCache;

            if (processNodeDataDistributionSamplesCache.Count() >= MinimumSampleCount)
            {
               TrainDataCache processNodeTrainSamplesCache = processNode.TrainSamplesCache;
               ImmutableList<SabotenCache> extractedDataDistributionSamplesCache = processNodeDataDistributionSamplesCache.Take(MinimumSampleCount).ToImmutableList();
               ImmutableList<SabotenCache> remainingDataDistributionSamplesCache = processNodeDataDistributionSamplesCache.Skip(MinimumSampleCount).ToImmutableList();

               Tuple<int, double, ImmutableList<SabotenCache>, ImmutableList<SabotenCache>> tuple = GetBestSplit(extractedDataDistributionSamplesCache, processNodeTrainSamplesCache.Samples, pakiraDecisionTreeModel);
               int bestFeatureIndex = tuple.Item1;
               double threshold = tuple.Item2;
               ImmutableList<SabotenCache> bestSplitDataDistributionSamplesCache = tuple.Item3;
               ImmutableList<SabotenCache> bestSplitTrainSamplesCache = tuple.Item4;

               remainingDataDistributionSamplesCache = pakiraDecisionTreeModel.Prefetch(remainingDataDistributionSamplesCache, bestFeatureIndex);

               ImmutableList<SabotenCache> concatenatedDataDistributionSamples = bestSplitDataDistributionSamplesCache.Concat(remainingDataDistributionSamplesCache).ToImmutableList();

               concatenatedDataDistributionSamples.Count().ShouldBeGreaterThan(0);

               PakiraNode node = new PakiraNode(bestFeatureIndex, threshold);

               for (int leafIndex = 0; leafIndex < 2; leafIndex++)
               {
                  bool theKey = (leafIndex == 0);

                  sampleSliceCache[leafIndex] = concatenatedDataDistributionSamples.Where(column => ThresholdCompareLessThanOrEqual(column[bestFeatureIndex], threshold) == theKey).ToImmutableList();

                  slice[leafIndex] = bestSplitTrainSamplesCache.Where(column => ThresholdCompareLessThanOrEqual(column[bestFeatureIndex], threshold) == theKey).ToImmutableList();

                  ySlice[leafIndex] = processNodeTrainSamplesCache.Labels.Where(
                              (trainLabel, trainLabelIndex) =>
                              {
                                 double trainSample = bestSplitTrainSamplesCache.ElementAt(trainLabelIndex)[bestFeatureIndex];

                                 return ThresholdCompareLessThanOrEqual(trainSample, threshold) == theKey;
                              }
                              ).ToImmutableList();
               }

               for (int leafIndex = 0; leafIndex < 2; leafIndex++)
               {
                  if (slice[leafIndex].Count() > 0)
                  {
                     int distinctLabelsCount = ySlice[leafIndex].Distinct().Count();

                     // only one answer, set leaf
                     if (distinctLabelsCount == 1)
                     {
                        double leafValue = ySlice[leafIndex].First();

                        leaves[leafIndex] = new PakiraLeaf(leafValue);
                     }
                     // otherwise continue to build tree
                     else
                     {
                        leaves[leafIndex] = new PakiraLeaf(UNKNOWN_CLASS_INDEX);

                        processNodes.Push(new ProcessNode(leaves[leafIndex], new TrainDataCache(slice[leafIndex], ySlice[leafIndex]), sampleSliceCache[leafIndex]));
                     }
                  }
                  else
                  {
                     // We don't have any training data for this node
                     leaves[leafIndex] = new PakiraLeaf(UNKNOWN_CLASS_INDEX);
                  }
               }

               pakiraDecisionTreeModel = pakiraDecisionTreeModel.RemoveDataDistributionSamplesCache(processNode.Node as PakiraLeaf);
               pakiraTree = pakiraTree.ReplaceLeaf(processNode.Node as PakiraLeaf, PakiraTree.Empty.AddNode(node, leaves[0], leaves[1]));
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataDistributionSamplesCache(leaves[0], sampleSliceCache[0]);
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataDistributionSamplesCache(leaves[1], sampleSliceCache[1]);
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.RemoveTrainDataCache(processNode.Node as PakiraLeaf);
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(leaves[0], new TrainDataCache(slice[0], ySlice[0]));
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(leaves[1], new TrainDataCache(slice[1], ySlice[1]));
            }
            else
            {
               PakiraLeaf pakiraLeaf = new PakiraLeaf(INSUFFICIENT_SAMPLES_CLASS_INDEX);

               pakiraDecisionTreeModel = pakiraDecisionTreeModel.RemoveDataDistributionSamplesCache(processNode.Node as PakiraLeaf);
               pakiraTree = pakiraTree.ReplaceLeaf(processNode.Node as PakiraLeaf, PakiraTree.Empty.AddLeaf(pakiraLeaf));
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataDistributionSamplesCache(pakiraLeaf, processNodeDataDistributionSamplesCache);
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.RemoveTrainDataCache(processNode.Node as PakiraLeaf);
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(pakiraLeaf, processNode.TrainSamplesCache);
            }
         }

         return pakiraDecisionTreeModel.UpdateTree(pakiraTree);
      }

      static double histogramLowerBound = (0.0).Decrement();

      private Tuple<int, double, ImmutableList<SabotenCache>, ImmutableList<SabotenCache>> GetBestSplit(ImmutableList<SabotenCache> extractedDataDistributionSamplesCache, ImmutableList<SabotenCache> extractedTrainSamplesCache, PakiraDecisionTreeModel pakiraDecisionTreeModel)
      {
         ImmutableList<SabotenCache> extractedDataDistributionSamplesCacheList = extractedDataDistributionSamplesCache.ToImmutableList();
         ImmutableList<int> randomFeatureIndices = pakiraDecisionTreeModel.FeatureIndices().Shuffle(RandomSource).ToImmutableList();

         double bestScore = -1.0;
         int bestFeature = -1;
         ImmutableList<double> bestFeatureDataDistributionSample = ImmutableList<double>.Empty;

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

            if (histogram.LowerBound > 0.0)
            {
               int histogramIndex = Enumerable.Range(0, histogram.BucketCount).First((index) => histogram[index].Count > 0);

               // LowerBound always has an offset, so we need Decrement()
               histogram.AddBucket(new Bucket(histogramLowerBound, histogram.LowerBound, Math.Max(0, histogram[histogramIndex].Count - 1)));
            }

            if (histogram.UpperBound < 255.0)
            {
               int histogramIndex = Enumerable.Range(0, histogram.BucketCount).Reverse().First((index) => histogram[index].Count > 0);

               histogram.AddBucket(new Bucket(histogram.UpperBound, 255.0, Math.Max(0, histogram[histogramIndex].Count - 1)));
            }

            extractedTrainSamplesCache = pakiraDecisionTreeModel.Prefetch(extractedTrainSamplesCache, featureIndex);

            score = extractedTrainSamplesCache.Max((SabotenCache trainSample) =>
            {
               double trainSampleValue = trainSample[featureIndex];

               trainSampleValue.ShouldBeGreaterThanOrEqualTo(0.0);
               trainSampleValue.ShouldBeLessThanOrEqualTo(255.0);

               double bucketCount = histogram.GetBucketOf(trainSampleValue).Count;

               return count - bucketCount;
            }
            );

            score /= count;

            if (score > bestScore)
            {
               bestScore = score;
               bestFeature = featureIndex;
               bestFeatureDataDistributionSample = featureDataDistributionSample;
            }

            if (score >= CertaintyScore)
            {
               break;
            }
         }

         bestFeature.ShouldBeGreaterThanOrEqualTo(0);

         double bestFeatureAverage = bestFeatureDataDistributionSample.Mean();

         return new Tuple<int, double, ImmutableList<SabotenCache>, ImmutableList<SabotenCache>>(bestFeature, bestFeatureAverage, extractedDataDistributionSamplesCacheList, extractedTrainSamplesCache);
      }
   }
}