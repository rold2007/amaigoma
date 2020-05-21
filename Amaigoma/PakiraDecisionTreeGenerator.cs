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

         Tuple<int, double, PakiraRange[]> tuple = GetBestSplit(extractedDataDistributionSamples, trainSamples);
         int bestFeatureIndex = tuple.Item1;
         double gain = tuple.Item2;
         PakiraRange[] segments = tuple.Item3;

         PakiraNode node = new PakiraNode
         {
            Column = bestFeatureIndex,
            Gain = gain,
            IsLeaf = false,
         };

         segments.Length.ShouldBe(2);

         // populate edges
         List<PakiraEdge> edges = new List<PakiraEdge>(segments.Length);

         for (int i = 0; i < segments.Length; i++)
         {
            // working set
            PakiraRange segment = segments[i];
            PakiraEdge edge = new PakiraEdge()
            {
               ParentId = node.Id,
               Min = segment.Min,
               Max = segment.Max,
               Label = string.Format("{0} <= x < {1}", segment.Min, segment.Max)
            };

            IEnumerable<IList<double>> sampleSlice = dataDistributionSamples.Where(column => column[bestFeatureIndex] >= segment.Min && column[bestFeatureIndex] < segment.Max);

            int sampleSliceCount = sampleSlice.Count();
            IEnumerable<IList<double>> slice = trainSamples.Where(column => column[bestFeatureIndex] >= segment.Min && column[bestFeatureIndex] < segment.Max);
            PakiraNode child;

            if (slice.Count() > 0)
            {
               IEnumerable<double> ySlice = trainLabels.Where(
               (trainLabel, trainLabelIndex) =>
               {
                  double trainSample = trainSamples.ElementAt(trainLabelIndex)[bestFeatureIndex];

                  return trainSample >= segment.Min && trainSample < segment.Max;
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
            edge.ChildId = child.Id;

            edges.Add(edge);
         }

         tree.AddNode(node);

         if (edges.Count > 1)
         {
            foreach (PakiraEdge edge in edges)
            {
               tree.AddEdge(edge);
            }
         }

         return node;
      }

      private Tuple<int, double, PakiraRange[]> GetBestSplit(IEnumerable<IList<double>> dataDistributionSamples, IEnumerable<IList<double>> trainSamples)
      {
         // Transpose the matrix to access one feature at a time
         Matrix<double> dataDistributionSamplesMatrix = Matrix<double>.Build.DenseOfColumns(dataDistributionSamples);
         PakiraRange[] bestSegments = null;
         int featureCount = dataDistributionSamplesMatrix.RowCount;
         double[] gains = new double[featureCount];

         Parallel.For(0, featureCount, featureIndex =>
         {
            double gain = 1.0;

            foreach(IList<double> trainSample in trainSamples)
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

         bestSegments = new PakiraRange[] { new PakiraRange(double.MinValue, bestFeatureAverage), new PakiraRange(bestFeatureAverage, double.MaxValue) };

         return new Tuple<int, double, PakiraRange[]>(bestFeature, bestGain, bestSegments);
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