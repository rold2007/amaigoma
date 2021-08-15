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

   using DataTransformer = System.Converter<System.Collections.Generic.IList<double>, System.Collections.Generic.IList<double>>;

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
      static private PassThroughTransformer DefaultDataTransformer = new PassThroughTransformer();
      private Random RandomSource = new Random();

      public PakiraDecisionTreeGenerator()
      {
         MinimumSampleCount = MINIMUM_SAMPLE_COUNT;
         CertaintyScore = DEFAULT_CERTAINTY_SCORE;
         DataTransformers += DefaultDataTransformer.ConvertAll;
      }

      public int MinimumSampleCount { get; set; }

      public double CertaintyScore { get; set; }

      public DataTransformer DataTransformers { get; set; }

      public void Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<IList<double>> trainSamples, IList<double> trainLabels)
      {
         DiscreteUniform discreteUniform = new DiscreteUniform(0, 255, RandomSource);
         IList<double> trainSample = trainSamples.ElementAt(0);
         int featureCount = trainSample.Count();
         bool generateMoreData = true;
         int dataDistributionSamplesCount = MinimumSampleCount;
         ImmutableList<SabotenCache> trainSamplesCache = trainSamples.Select(d => new SabotenCache(d)).ToImmutableList();
         TanukiTransformers theTransformers = new TanukiTransformers(DataTransformers, trainSample);

         Matrix<double> dataDistributionSamples = Matrix<double>.Build.Dense(dataDistributionSamplesCount, featureCount, (i, j) => discreteUniform.Sample());
         ImmutableList<SabotenCache> dataDistributionSamplesCache = dataDistributionSamples.EnumerateRows().Select(d => new SabotenCache(d)).ToImmutableList();

         pakiraDecisionTreeModel.DataTransformers = theTransformers;

         while (generateMoreData)
         {
            generateMoreData = false;

            pakiraDecisionTreeModel.Tree = BuildTree(trainSamplesCache, trainLabels.ToImmutableList(), dataDistributionSamplesCache, theTransformers);

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

      private PakiraTree BuildTree(ImmutableList<SabotenCache> trainSamplesCache, ImmutableList<double> trainLabels, ImmutableList<SabotenCache> dataDistributionSamplesCache, TanukiTransformers theTransformers)
      {
         int distinctCount = trainLabels.Distinct().Take(2).Count();

         distinctCount.ShouldBeGreaterThanOrEqualTo(1);

         if (distinctCount == 1)
         {
            return PakiraTree.Empty;
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
               Tuple<int, double, ImmutableList<SabotenCache>, ImmutableList<SabotenCache>> tuple = GetBestSplit(extractedDataDistributionSamplesCache, processNodeTrainSamplesCache, theTransformers);
               int bestFeatureIndex = tuple.Item1;
               double threshold = tuple.Item2;
               ImmutableList<SabotenCache> bestSplitDataDistributionSamplesCache = tuple.Item3;
               ImmutableList<SabotenCache> bestSplitTrainSamplesCache = tuple.Item4;

               ImmutableList<SabotenCache> concatenatedDataDistributionSamples = bestSplitDataDistributionSamplesCache.Concat(processNodeDataDistributionSamplesCache.Skip(MinimumSampleCount)).ToImmutableList();

               PakiraNode node = new PakiraNode(bestFeatureIndex, threshold);

               Func<double, double, bool>[] compareFunctions = { ThresholdCompareLessThanOrEqual, ThresholdCompareGreater };

               concatenatedDataDistributionSamples = concatenatedDataDistributionSamples.Prefetch(bestFeatureIndex, theTransformers).ToImmutableList();

               for (int i = 0; i < compareFunctions.Length; i++)
               {
                  ImmutableList<SabotenCache> sampleSliceCache = concatenatedDataDistributionSamples.Where(column => compareFunctions[i](column[bestFeatureIndex], threshold)).ToImmutableList();

                  ImmutableList<SabotenCache> slice = bestSplitTrainSamplesCache.Where(column => compareFunctions[i](column[bestFeatureIndex], threshold)).ToImmutableList();

                  if (slice.Count() > 0)
                  {
                     ImmutableList<double> ySlice = processNodeTrainLabels.Where(
                     (trainLabel, trainLabelIndex) =>
                     {
                        double trainSample = bestSplitTrainSamplesCache.ElementAt(trainLabelIndex)[bestFeatureIndex];

                        return compareFunctions[i](trainSample, threshold);
                     }
                     ).ToImmutableList();

                     ImmutableList<double> distinctLabels = ySlice.Distinct().ToImmutableList();
                     int distinctLabelsCount = distinctLabels.Count();

                     // only one answer, set leaf
                     if (distinctLabelsCount == 1)
                     {
                        double leafValue = ySlice.First();

                        leaves[i] = new PakiraLeaf(leafValue);
                     }
                     // otherwise continue to build tree
                     else
                     {
                        leaves[i] = new PakiraLeaf(UNKNOWN_CLASS_INDEX);
                        processNodes.Push(new ProcessNode(leaves[i], slice, ySlice, sampleSliceCache));
                     }
                  }
                  else
                  {
                     // We don't have any training data for this node
                     leaves[i] = new PakiraLeaf(UNKNOWN_CLASS_INDEX);
                  }
               }

               pakiraTree = pakiraTree.ReplaceLeaf(processNode.Node as PakiraLeaf, PakiraTree.Empty.AddNode(node, leaves[0], leaves[1]));
            }
            else
            {
               pakiraTree = pakiraTree.ReplaceLeaf(processNode.Node as PakiraLeaf, PakiraTree.Empty.AddLeaf(new PakiraLeaf(INSUFFICIENT_SAMPLES_CLASS_INDEX)));
            }
         }

         return pakiraTree;
      }

      private Tuple<int, double, ImmutableList<SabotenCache>, ImmutableList<SabotenCache>> GetBestSplit(ImmutableList<SabotenCache> extractedDataDistributionSamplesCache, ImmutableList<SabotenCache> extractedTrainSamplesCache, TanukiTransformers theTransformers)
      {
         ImmutableList<SabotenCache> extractedDataDistributionSamplesCacheList = extractedDataDistributionSamplesCache.ToImmutableList();
         ImmutableList<int> randomFeatureIndices = Enumerable.Range(0, theTransformers.TotalOutputSamples).Shuffle(RandomSource).ToImmutableList();

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