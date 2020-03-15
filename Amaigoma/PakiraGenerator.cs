namespace Amaigoma
{
   using MathNet.Numerics.Distributions;
   using MathNet.Numerics.LinearAlgebra;
   using MathNet.Numerics.Statistics;
   using numl.Data;
   using numl.Math.LinearAlgebra;
   using numl.Supervised.DecisionTree;
   using System;
   using System.Collections.Generic;
   using System.Diagnostics;
   using System.Linq;
   using System.Threading.Tasks;

   public class PakiraGenerator : PakiraDecisionTreeGenerator
   {
      static public int UNKNOWN_CLASS_INDEX = -1;
      static public int INSUFFICIENT_SAMPLES_CLASS_INDEX = -2;

      public PakiraGenerator()
      {
      }

      public PakiraGenerator(IEnumerable<object> samples, int minimumSampleCount) : this()
      {
         //Samples = samples;
         ImpurityType = typeof(DispersionError);
         MinimumSampleCount = minimumSampleCount;
      }

      public int MinimumSampleCount { get; set; }

      public void Generate(PakiraModel pakiraModel, Matrix<double> trainSamples, Vector<double> trainLabels)
      {
         ContinuousUniform continuousUniform = new ContinuousUniform(0, 256);
         int featureCount = trainSamples.RowCount;
         Matrix<double> dataDistributionSamples = Matrix<double>.Build.Dense(featureCount, 1000, (i, j) => continuousUniform.Sample());

         pakiraModel.Tree.Root = BuildTree(pakiraModel, trainSamples.EnumerateColumns(), trainLabels.Enumerate(), dataDistributionSamples.EnumerateColumns());
      }

      private Node BuildTree(PakiraModel pakiraModel, IEnumerable<Vector<double>> trainSamples, IEnumerable<double> trainLabels, IEnumerable<Vector<double>> dataDistributionSamples)
      {
         Tree tree = pakiraModel.Tree;
         Matrix<double> dataDistributionSamplesMatrix = Matrix<double>.Build.DenseOfColumns(dataDistributionSamples.Take(1000));
         Matrix<double> trainSamplesMatrix = Matrix<double>.Build.DenseOfColumns(trainSamples);

         Tuple<int, double, numl.Math.Range[]> tuple = GetBestSplit(dataDistributionSamplesMatrix, trainSamplesMatrix);
         int bestFeatureIndex = tuple.Item1;
         double gain = tuple.Item2;
         numl.Math.Range[] segments = tuple.Item3;

         // Cannot find a split in the samples.
         // Not enough samples or all samples are identical.
         if (bestFeatureIndex == -1)
         {
            return BuildLeafNode(INSUFFICIENT_SAMPLES_CLASS_INDEX);
         }

         Node node = new Node
         {
            Column = bestFeatureIndex,
            Gain = gain,
            IsLeaf = false,
         };

         // populate edges
         List<Edge> edges = new List<Edge>(segments.Length);

         for (int i = 0; i < segments.Length; i++)
         {
            // working set
            numl.Math.Range segment = segments[i];
            Edge edge = new Edge()
            {
               ParentId = node.Id,
               Discrete = false,
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
                     Node child = BuildLeafNode(ySlice.First());

                     tree.AddVertex(child);
                     edge.ChildId = child.Id;
                  }
                  // otherwise continue to build tree
                  else
                  {
                     Node child = BuildTree(pakiraModel, slice, ySlice, sampleSlice);

                     tree.AddVertex(child);
                     edge.ChildId = child.Id;
                  }

                  edges.Add(edge);
               }
               else
               {
                  // We don't have any training data for this node
                  Node child = BuildLeafNode(INSUFFICIENT_SAMPLES_CLASS_INDEX);

                  tree.AddVertex(child);
                  edge.ChildId = child.Id;
                  edges.Add(edge);
               }
            }
            else
            {
               // We don't have enough sample data for this node
               Node child = BuildLeafNode(INSUFFICIENT_SAMPLES_CLASS_INDEX);

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
            foreach (Edge edge in edges)
            {
               tree.AddEdge(edge);
            }
         }

         return node;
      }

      private Tuple<int, double, numl.Math.Range[]> GetBestSplit(Matrix<double> samples, Matrix<double> x)
      {
         numl.Math.Range[] bestSegments = null;
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

         bestSegments = new numl.Math.Range[] { new numl.Math.Range(double.MinValue, bestFeatureAverage), new numl.Math.Range(bestFeatureAverage, double.MaxValue) };

         return new Tuple<int, double, numl.Math.Range[]>(bestFeature, bestGain, bestSegments);
      }

      private Node BuildLeafNode(double val)
      {
         return new Node()
         {
            IsLeaf = true,
            Value = val
         };
      }
   }
}

namespace ExtensionMethods
{
   using numl.Math.LinearAlgebra;
   using System;
   using System.Collections.Generic;
   using System.Linq;

   public class MySampling
   {
      /// <summary>
      /// Produce a uniform random sample from the open interval (min, max). The method will not return
      /// either end point
      /// </summary>
      /// <param name="min"></param>
      /// <param name="max"></param>
      /// <returns></returns>
      public static double GetUniform(double min = 0d, double max = 1.0d)
      {
         return (min + (numl.Math.Probability.Sampling.GetUniform() * ((max - min))));
      }
   }

   public static class MyConversions
   {
      private static Matrix Build(double[][] x)
      {
         // rows
         int n = x.Length;
         if (n == 0)
            throw new InvalidOperationException("Empty matrix (n)");

         // cols (being nice here...)
         var cols = x.Select(v => v.Length);
         int d = cols.Max();

         if (d == 0)
            throw new InvalidOperationException("Empty matrix (d)");

         // total zeros in matrix
         var zeros = (from v in x select v.Count(i => i == 0)).Sum();

         // if irregularities in jagged matrix, need to 
         // pad rows with less columns with additional
         // zeros by subtracting max width with each
         // individual row and getting the sum
         var pad = cols.Select(c => d - c).Sum();

         // check sparsity
         //var percent = (decimal)(zeros + pad) / (decimal)(n * d);

         Matrix m = Matrix.Zeros(n, d);

         return m;
      }

      public static Matrix ToMatrixParallel(this IEnumerable<IEnumerable<double>> matrix)
      {
         // materialize
         //double[][] x = (from v in matrix select v.ToArray()).ToArray();
         double[][] x = (from v in matrix.AsParallel() select v.ToArray()).ToArray();

         // determine matrix
         // size and type
         var m = Build(x);

         // fill 'er up!
         for (int i = 0; i < m.Rows; i++)
            for (int j = 0; j < m.Cols; j++)
               if (j >= x[i].Length)  // over bound limits
                  m[i, j] = 0;       // pad overflow to 0
               else
                  m[i, j] = x[i][j];

         return m;
      }
   }
}
