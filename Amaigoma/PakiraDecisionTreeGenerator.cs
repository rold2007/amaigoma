namespace Amaigoma
{
   using MathNet.Numerics.Distributions;
   using MathNet.Numerics.LinearAlgebra;
   using MathNet.Numerics.Statistics;
   using System;
   using System.Collections.Generic;
   using System.Diagnostics;
   using System.Linq;
   using System.Threading.Tasks;

   public class PakiraDecisionTreeGenerator
   {
      static public int UNKNOWN_CLASS_INDEX = -1;
      static public int INSUFFICIENT_SAMPLES_CLASS_INDEX = -2;
      static private int MINIMUM_SAMPLE_COUNT = 1000;

      public PakiraDecisionTreeGenerator()
      {
         MinimumSampleCount = MINIMUM_SAMPLE_COUNT;
      }

      public int MinimumSampleCount { get; set; }

      public void Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, Matrix<double> trainSamples, Vector<double> trainLabels)
      {
         ContinuousUniform continuousUniform = new ContinuousUniform(0, 256);
         int featureCount = trainSamples.RowCount;
         Matrix<double> dataDistributionSamples = Matrix<double>.Build.Dense(featureCount, 1000, (i, j) => continuousUniform.Sample());

         pakiraDecisionTreeModel.Tree.Root = BuildTree(pakiraDecisionTreeModel, trainSamples.EnumerateColumns(), trainLabels.Enumerate(), dataDistributionSamples.EnumerateColumns());
      }

      private PakiraNode BuildTree(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<Vector<double>> trainSamples, IEnumerable<double> trainLabels, IEnumerable<Vector<double>> dataDistributionSamples)
      {
         PakiraTree tree = pakiraDecisionTreeModel.Tree;
         Matrix<double> dataDistributionSamplesMatrix = Matrix<double>.Build.DenseOfColumns(dataDistributionSamples.Take(1000));
         Matrix<double> trainSamplesMatrix = Matrix<double>.Build.DenseOfColumns(trainSamples);

         Tuple<int, double, PakiraRange[]> tuple = GetBestSplit(dataDistributionSamplesMatrix, trainSamplesMatrix);
         int bestFeatureIndex = tuple.Item1;
         double gain = tuple.Item2;
         PakiraRange[] segments = tuple.Item3;

         // Cannot find a split in the samples.
         // Not enough samples or all samples are identical.
         if (bestFeatureIndex == -1)
         {
            return BuildLeafNode(INSUFFICIENT_SAMPLES_CLASS_INDEX);
         }

         PakiraNode node = new PakiraNode
         {
            Column = bestFeatureIndex,
            Gain = gain,
            IsLeaf = false,
         };

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
               Max = segment.Max
            };

            // get range label
            edge.Label = string.Format("{0} <= x < {1}", segment.Min, segment.Max);

            IEnumerable<Vector<double>> sampleSlice = dataDistributionSamples.Where(column => column[bestFeatureIndex] >= segment.Min && column[bestFeatureIndex] < segment.Max);

            int sampleSliceCount = sampleSlice.Count();

            if (sampleSliceCount >= MinimumSampleCount)
            {
               IEnumerable<Vector<double>> slice = trainSamples.Where(column => column[bestFeatureIndex] >= segment.Min && column[bestFeatureIndex] < segment.Max);

               int sliceCount = slice.Count();

               if (sliceCount > 0)
               {
                  IEnumerable<double> ySlice = trainLabels.Where(
                  (trainLabel, trainLabelIndex) =>
                  {
                     double trainSample = trainSamples.ElementAt(trainLabelIndex).At(bestFeatureIndex);

                     return trainSample >= segment.Min && trainSample < segment.Max;
                  }
                  );

                  int labelCount = ySlice.Distinct().Count();

                  // only one answer, set leaf
                  if (labelCount == 1)
                  {
                     PakiraNode child = BuildLeafNode(ySlice.First());

                     tree.AddVertex(child);
                     edge.ChildId = child.Id;
                  }
                  // otherwise continue to build tree
                  else
                  {
                     PakiraNode child = BuildTree(pakiraDecisionTreeModel, slice, ySlice, sampleSlice);

                     tree.AddVertex(child);
                     edge.ChildId = child.Id;
                  }

                  edges.Add(edge);
               }
               else
               {
                  // We don't have any training data for this node
                  PakiraNode child = BuildLeafNode(INSUFFICIENT_SAMPLES_CLASS_INDEX);

                  tree.AddVertex(child);
                  edge.ChildId = child.Id;
                  edges.Add(edge);
               }
            }
            else
            {
               // We don't have enough sample data for this node
               PakiraNode child = BuildLeafNode(INSUFFICIENT_SAMPLES_CLASS_INDEX);

               tree.AddVertex(child);
               edge.ChildId = child.Id;
               edges.Add(edge);
            }
         }

         // problem, need to convert
         // parent to terminal node
         // with mode
         if (edges.Count <= 1)
         {
            Debug.Fail("Need to decide how to replace this.");

            //double val = y.Mode();

            //node.IsLeaf = true;
            //node.Value = val;
         }

         tree.AddVertex(node);

         if (edges.Count > 1)
         {
            foreach (PakiraEdge edge in edges)
            {
               tree.AddEdge(edge);
            }
         }

         return node;
      }

      private Tuple<int, double, PakiraRange[]> GetBestSplit(Matrix<double> samples, Matrix<double> x)
      {
         PakiraRange[] bestSegments = null;
         double[] gains = new double[samples.RowCount];

         Parallel.For(0, samples.RowCount, featureIndex =>
         {
            double gain = 1.0;

            for (int sampleIndex = 0; sampleIndex < x.ColumnCount; sampleIndex++)
            {
               double rowValue = x.At(sampleIndex, featureIndex);
               double quantileRank = samples.Row(featureIndex).QuantileRank(rowValue, RankDefinition.Default);

               // Keep the sample farthest in the data distribution
               gain = Math.Min(gain, Math.Min(quantileRank, 1.0 - quantileRank));
            }

            gains[featureIndex] = gain;
         }
         );

         double bestGain = 1.0;
         int bestFeature = -1;

         for (int featureIndex = 0; featureIndex < samples.RowCount; featureIndex++)
         {
            double gain = gains[featureIndex];

            if (gain < bestGain)
            {
               bestGain = gain;
               bestFeature = featureIndex;
            }
         }

         double bestFeatureAverage = samples.Row(bestFeature).Mean();

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