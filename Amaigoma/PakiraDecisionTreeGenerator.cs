namespace Amaigoma
{
   using MathNet.Numerics.Distributions;
   using MathNet.Numerics.LinearAlgebra;
   using MathNet.Numerics.Statistics;
   using Shouldly;
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using System.Threading.Tasks;

   public class PakiraDecisionTreeGenerator
   {
      static public int UNKNOWN_CLASS_INDEX = -1;
      static public int INSUFFICIENT_SAMPLES_CLASS_INDEX = -2;
      static private int MINIMUM_SAMPLE_COUNT = 1000;
      private PassThroughTransformer DefaultDataTransformer = new PassThroughTransformer();

      public PakiraDecisionTreeGenerator()
      {
         MinimumSampleCount = MINIMUM_SAMPLE_COUNT;
      }

      public int MinimumSampleCount { get; set; }

      public void Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<IList<double>> trainSamples, IList<double> trainLabels)
      {
         Generate(pakiraDecisionTreeModel, trainSamples, trainLabels, DefaultDataTransformer.ConvertAll);
      }

      public void Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<IList<double>> trainSamples, IList<double> trainLabels, Converter<IList<double>, IList<double>> dataTransformers)
      {
         ContinuousUniform continuousUniform = new ContinuousUniform(0, 256);
         int featureCount = trainSamples.ElementAt(0).Count();
         bool generateMoreData = true;
         int dataDistributionSamplesCount = MinimumSampleCount * 3;
         List<IList<double>> transformedTrainSamples = trainSamples.Select(d => dataTransformers(d)).ToList();

         while (generateMoreData)
         {
            Matrix<double> dataDistributionSamples = Matrix<double>.Build.Dense(dataDistributionSamplesCount, featureCount, (i, j) => continuousUniform.Sample());
            List<IList<double>> transformedDataDistributionSamples = dataDistributionSamples.EnumerateRows().Select(d => dataTransformers(d)).ToList();

            generateMoreData = false;
            pakiraDecisionTreeModel.Tree.Clear();

            pakiraDecisionTreeModel.Tree.Root = BuildTree(pakiraDecisionTreeModel, transformedTrainSamples, trainLabels, transformedDataDistributionSamples);

            generateMoreData = pakiraDecisionTreeModel.Tree.GetNodes().Any(pakiraNode => (pakiraNode.IsLeaf && pakiraNode.Value == INSUFFICIENT_SAMPLES_CLASS_INDEX));

            dataDistributionSamplesCount *= 2;
         }

         pakiraDecisionTreeModel.DataTransformers = dataTransformers;
      }

      static private bool ThresholdCompareLessThanOrEqual(double inputValue, double threshold)
      {
         return inputValue <= threshold;
      }

      static private bool ThresholdCompareGreater(double inputValue, double threshold)
      {
         return inputValue > threshold;
      }

      private PakiraNode BuildTree(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<IList<double>> trainSamples, IEnumerable<double> trainLabels, IEnumerable<IList<double>> dataDistributionSamples)
      {
         PakiraTree tree = pakiraDecisionTreeModel.Tree;
         IEnumerable<IList<double>> extractedDataDistributionSamples = dataDistributionSamples.Take(MinimumSampleCount);

         int extractedDataDistributionSamplesCount = extractedDataDistributionSamples.Count();

         if (extractedDataDistributionSamplesCount < MinimumSampleCount)
         {
            PakiraNode child = BuildLeafNode(INSUFFICIENT_SAMPLES_CLASS_INDEX);

            tree.AddNode(child);

            return child;
         }

         Tuple<int, double, double> tuple = GetBestSplit(extractedDataDistributionSamples, trainSamples);
         int bestFeatureIndex = tuple.Item1;
         double gain = tuple.Item2;
         double threshold = tuple.Item3;

         PakiraNode node = new PakiraNode
         {
            Column = bestFeatureIndex,
            Gain = gain,
            IsLeaf = false,
            Threshold = threshold
         };

         Func<double, double, bool>[] compareFunctions = { ThresholdCompareLessThanOrEqual, ThresholdCompareGreater };

         for (int i = 0; i < compareFunctions.Length; i++)
         {
            IEnumerable<IList<double>> sampleSlice = dataDistributionSamples.Where(column => compareFunctions[i](column[bestFeatureIndex], threshold));

            int sampleSliceCount = sampleSlice.Count();
            IEnumerable<IList<double>> slice = trainSamples.Where(column => compareFunctions[i](column[bestFeatureIndex], threshold));
            PakiraNode child;

            if (slice.Count() > 0)
            {
               IEnumerable<double> ySlice = trainLabels.Where(
               (trainLabel, trainLabelIndex) =>
               {
                  double trainSample = trainSamples.ElementAt(trainLabelIndex)[bestFeatureIndex];

                  return compareFunctions[i](trainSample, threshold);
               }
               );

               int labelCount = ySlice.Distinct().Count();

               // only one answer, set leaf
               if (labelCount == 1)
               {
                  child = BuildLeafNode(ySlice.First());
               }
               // otherwise continue to build tree
               else
               {
                  child = BuildTree(pakiraDecisionTreeModel, slice, ySlice, sampleSlice);
               }
            }
            else
            {
               // We don't have any training data for this node
               child = BuildLeafNode(UNKNOWN_CLASS_INDEX);
            }

            tree.AddNode(child);

            node.ChildId[i] = child.Id;
         }

         tree.AddNode(node);

         return node;
      }

      private Tuple<int, double, double> GetBestSplit(IEnumerable<IList<double>> dataDistributionSamples, IEnumerable<IList<double>> trainSamples)
      {
         // Transpose the matrix to access one feature at a time
         Matrix<double> dataDistributionSamplesMatrix = Matrix<double>.Build.DenseOfColumns(dataDistributionSamples);
         int featureCount = dataDistributionSamplesMatrix.RowCount;
         double[] gains = new double[featureCount];

         Parallel.For(0, featureCount, featureIndex =>
         {
            double gain = 1.0;

            foreach (IList<double> trainSample in trainSamples)
            {
               double trainSampleValue = trainSample[featureIndex];
               double quantileRank = dataDistributionSamplesMatrix.Row(featureIndex).QuantileRank(trainSampleValue, RankDefinition.Default);

               // Keep the sample farthest in the data distribution
               gain = Math.Min(gain, Math.Min(quantileRank, 1.0 - quantileRank));
            }

            gains[featureIndex] = gain;
         }
         );

         double bestGain = 1.0;
         int bestFeature = -1;

         for (int featureIndex = 0; featureIndex < featureCount; featureIndex++)
         {
            double gain = gains[featureIndex];

            if (gain < bestGain)
            {
               bestGain = gain;
               bestFeature = featureIndex;
            }
         }

         bestFeature.ShouldBeGreaterThanOrEqualTo(0);

         double bestFeatureAverage = dataDistributionSamplesMatrix.Row(bestFeature).Mean();

         return new Tuple<int, double, double>(bestFeature, bestGain, bestFeatureAverage);
      }

      private PakiraNode BuildLeafNode(double val)
      {
         return new PakiraNode()
         {
            IsLeaf = true,
            Value = val
         };
      }
   }
}