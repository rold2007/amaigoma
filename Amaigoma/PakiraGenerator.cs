﻿namespace Amaigoma
{
   using ExtensionMethods;
   using MathNet.Numerics.LinearAlgebra;
   using numl.Data;
   using numl.Math;
   using numl.Math.LinearAlgebra;
   using numl.Model;
   using numl.Supervised;
   using numl.Supervised.DecisionTree;
   using numl.Utils;
   using Shouldly;
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

      //private IEnumerable<object> samples;
      //private Matrix convertedSamples;

      //public IEnumerable<object> Samples
      //{
      //   get
      //   {
      //      return samples;
      //   }

      //   set
      //   {
      //      samples = value;

      //      IEnumerable<IEnumerable<double>> doubles = Descriptor.Convert(value, false, false);

      //      convertedSamples = doubles.ToMatrixParallel();
      //   }
      //}

      public int MinimumSampleCount { get; set; }

      public void Generate(PakiraModel pakiraModel, IDataProvider dataProvider, Matrix<double> trainSamples, Vector<double> trainLabels)
      {
         PakiraDescriptor pakiraDescriptor = pakiraModel.Descriptor;

         if (pakiraDescriptor == null)
         {
            throw new InvalidOperationException("Cannot build decision tree without type knowledge!");
         }

         StringProperty labelsProperty = pakiraDescriptor.Label as StringProperty;
         List<string> labelsList = new List<string>(labelsProperty.Dictionary)
         {
            "Insufficient",
            "Unknown"
         };

         labelsProperty.Dictionary = labelsList.ToArray();

         pakiraModel.Tree.Root = BuildTree(pakiraModel, dataProvider, trainSamples, trainLabels, pakiraModel.Tree.Root);
      }

      /// <summary>Generate model based on a set of examples.</summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="examples">Example set.</param>
      /// <returns>Model.</returns>
      //public new PakiraModel Generate(IEnumerable<object> examples)
      //{
      //   if (examples.Count() == 0) throw new InvalidOperationException("Empty example set.");

      //   return Generate(examples) as PakiraModel;
      //}

      //public override BaseModel Generate(Matrix x, Vector y)
      //{
      //   StringProperty labelsProperty = Descriptor.Label as StringProperty;
      //   List<string> labelsList = new List<string>(labelsProperty.Dictionary)
      //   {
      //      "Insufficient",
      //      "Unknown"
      //   };

      //   labelsProperty.Dictionary = labelsList.ToArray();

      //   this.Preprocess(x);

      //   Tree tree = new Tree();

      //   tree.Root = BuildTree(convertedSamples, x, y, tree);

      //   return new PakiraModel
      //   {
      //      Descriptor = Descriptor,
      //      Tree = tree,
      //   };
      //}

      private Node BuildTree(PakiraModel pakiraModel, IDataProvider dataProvider, Matrix<double> trainSamples, Vector<double> trainLabels, IVertex currentVertex)
      {
         Tree tree = pakiraModel.Tree;
         Matrix<double> dataDistributionSamples = null;

         PakiraDescriptor Descriptor = pakiraModel.Descriptor;

         if (Descriptor == null)
            throw new InvalidOperationException("Cannot build decision tree without type knowledge!");
         //if (Descriptor.Features == null || Descriptor.Features.Length == 0)
         //   throw new InvalidOperationException("Invalid descriptor: Empty feature set!");
         if (Descriptor.Label == null)
            throw new InvalidOperationException("Invalid descriptor: Empty label!");

         int labelsCount = (Descriptor.Label as StringProperty).Dictionary.Length;

         Tuple<int, double, Range[]> tuple = GetBestSplit(dataDistributionSamples, trainSamples);
         int col = tuple.Item1;
         double gain = tuple.Item2;
         Range[] segments = tuple.Item3;

         // Cannot find a split in the samples.
         // Not enough samples or all samples are identical.
         if (col == -1)
         {
            return BuildLeafNode(labelsCount + INSUFFICIENT_SAMPLES_CLASS_INDEX);
         }

         Node node = new Node
         {
            Column = col,
            Gain = gain,
            IsLeaf = false,
            Name = Descriptor.ColumnAt(col)
         };

         // populate edges
         List<Edge> edges = new List<Edge>(segments.Length);

         for (int i = 0; i < segments.Length; i++)
         {
            // working set
            Range segment = segments[i];
            Edge edge = new Edge()
            {
               ParentId = node.Id,
               Discrete = false,
               Min = segment.Min,
               Max = segment.Max
            };

            //IEnumerable<int> samplesSlice = null;
            //IEnumerable<int> slice = null;

            if (edge.Discrete)
            {
               Debug.Assert(false, "Need to debug this and see if I want to do anything special here.");

               // get discrete label
               edge.Label = Descriptor.At(col).Convert(segment.Min).ToString();

               Debug.Fail("Need to convert this code.");
               //samplesSlice = dataDistributionSamples.Indices(v => v[col] == segment.Min);

               // do value check for matrix slicing
               Debug.Fail("Need to convert this code.");
               //slice = x.Indices(v => v[col] == segment.Min);
            }
            else
            {
               // get range label
               edge.Label = string.Format("{0} <= x < {1}", segment.Min, segment.Max);

               Debug.Fail("Need to convert this code.");
               //samplesSlice = dataDistributionSamples.Indices(v => v[col] >= segment.Min && v[col] < segment.Max);

               // do range check for matrix slicing
               Debug.Fail("Need to convert this code.");
               //slice = x.Indices(v => v[col] >= segment.Min && v[col] < segment.Max);
            }

            Debug.Fail("Need to convert this code.");
            //int sampleSliceCount = samplesSlice.Count();
            int sampleSliceCount = -1;

            if (sampleSliceCount >= MinimumSampleCount)
            {
               Debug.Fail("Need to convert this code.");
               //int sliceCount = slice.Count();
               int sliceCount = -1;

               if (sliceCount > 0)
               {
                  Debug.Fail("Need to convert this code.");
                  //Vector ySlice = y.Slice(slice);
                  Vector ySlice = null;

                  // only one answer, set leaf
                  if (ySlice.Distinct().Count() == 1)
                  {
                     Node child = BuildLeafNode(ySlice[0]);

                     tree.AddVertex(child);
                     edge.ChildId = child.Id;
                  }
                  // otherwise continue to build tree
                  else
                  {
                     Debug.Fail("Need to convert this code.");
                     //Node child = BuildTree(dataDistributionSamples.Slice(samplesSlice), x.Slice(slice), ySlice, tree);
                     Node child = null;

                     tree.AddVertex(child);
                     edge.ChildId = child.Id;
                  }

                  edges.Add(edge);
               }
               else
               {
                  // We don't have any training data for this node
                  Node child = BuildLeafNode(labelsCount + INSUFFICIENT_SAMPLES_CLASS_INDEX);

                  tree.AddVertex(child);
                  edge.ChildId = child.Id;
                  edges.Add(edge);
               }
            }
            else
            {
               // We don't have enough sample data for this node
               Node child = BuildLeafNode(labelsCount + INSUFFICIENT_SAMPLES_CLASS_INDEX);

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
            Debug.Assert(false, "Need to debug this and see if I want to do anything special here.");

            //double val = y.Mode();

            //node.IsLeaf = true;
            //node.Value = val;
         }

         tree.AddVertex(node);

         if (edges.Count > 1)
         {
            foreach (Edge e in edges)
            {
               tree.AddEdge(e);
            }
         }

         return node;
      }

      /// <summary>Builds a tree.</summary>
      /// <param name="x">The Matrix to process.</param>
      /// <param name="y">The Vector to process.</param>
      /// <param name="depth">The depth.</param>
      /// <returns>A Node.</returns>
      /*
      private Node BuildTree(Matrix samples, Matrix x, Vector y, Tree tree)
      {
         int labelsCount = (Descriptor.Label as StringProperty).Dictionary.Length;

         Tuple<int, double, Range[]> tuple = GetBestSplit(samples, x);
         int col = tuple.Item1;
         double gain = tuple.Item2;
         Range[] segments = tuple.Item3;

         // Cannot find a split in the samples.
         // Not enough samples or all samples are identical.
         if (col == -1)
         {
            return BuildLeafNode(labelsCount + INSUFFICIENT_SAMPLES_CLASS_INDEX);
         }

         Node node = new Node
         {
            Column = col,
            Gain = gain,
            IsLeaf = false,
            Name = Descriptor.ColumnAt(col)
         };

         // populate edges
         List<Edge> edges = new List<Edge>(segments.Length);

         for (int i = 0; i < segments.Length; i++)
         {
            // working set
            Range segment = segments[i];
            Edge edge = new Edge()
            {
               ParentId = node.Id,
               Discrete = false,
               Min = segment.Min,
               Max = segment.Max
            };

            IEnumerable<int> samplesSlice;
            IEnumerable<int> slice;

            if (edge.Discrete)
            {
               Debug.Assert(false, "Need to debug this and see if I want to do anything special here.");

               // get discrete label
               edge.Label = Descriptor.At(col).Convert(segment.Min).ToString();

               samplesSlice = samples.Indices(v => v[col] == segment.Min);

               // do value check for matrix slicing
               slice = x.Indices(v => v[col] == segment.Min);
            }
            else
            {
               // get range label
               edge.Label = string.Format("{0} <= x < {1}", segment.Min, segment.Max);

               samplesSlice = samples.Indices(v => v[col] >= segment.Min && v[col] < segment.Max);

               // do range check for matrix slicing
               slice = x.Indices(v => v[col] >= segment.Min && v[col] < segment.Max);
            }

            int sampleSliceCount = samplesSlice.Count();

            if (sampleSliceCount >= MinimumSampleCount)
            {
               int sliceCount = slice.Count();

               if (sliceCount > 0)
               {
                  Vector ySlice = y.Slice(slice);

                  // only one answer, set leaf
                  if (ySlice.Distinct().Count() == 1)
                  {
                     Node child = BuildLeafNode(ySlice[0]);

                     tree.AddVertex(child);
                     edge.ChildId = child.Id;
                  }
                  // otherwise continue to build tree
                  else
                  {
                     Node child = BuildTree(samples.Slice(samplesSlice), x.Slice(slice), ySlice, tree);

                     tree.AddVertex(child);
                     edge.ChildId = child.Id;
                  }

                  edges.Add(edge);
               }
               else
               {
                  // We don't have any training data for this node
                  Node child = BuildLeafNode(labelsCount + INSUFFICIENT_SAMPLES_CLASS_INDEX);

                  tree.AddVertex(child);
                  edge.ChildId = child.Id;
                  edges.Add(edge);
               }
            }
            else
            {
               // We don't have enough sample data for this node
               Node child = BuildLeafNode(labelsCount + INSUFFICIENT_SAMPLES_CLASS_INDEX);

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
            Debug.Assert(false, "Need to debug this and see if I want to do anything special here.");

            double val = y.Mode();

            node.IsLeaf = true;
            node.Value = val;
         }

         tree.AddVertex(node);

         if (edges.Count > 1)
         {
            foreach (Edge e in edges)
            {
               tree.AddEdge(e);
            }
         }

         return node;
      }
      */

      private Tuple<int, double, Range[]> GetBestSplit(Matrix<double> samples, Matrix<double> x)
      {
         Range[] bestSegments = null;

         Debug.Fail("Need to convert this code.");
         Summary featureProperties = new Summary()
         {
            //Average = samples.Mean(VectorType.Row),
            //StandardDeviation = samples.StdDev(VectorType.Row),
            //Minimum = samples.Min(VectorType.Row),
            //Maximum = samples.Max(VectorType.Row),
         };

         Debug.Fail("Need to convert this code.");
         //double[] gains = new double[samples.Cols];
         double[] gains = null;

         Parallel.For(0, samples.ColumnCount, col =>
         {
            double gain = 0;

            double average = featureProperties.Average[col];
            double standardDeviation = featureProperties.StandardDeviation[col];

            if (standardDeviation > double.Epsilon)
            {
               for (int row = 0; row < x.RowCount; row++)
               {
                  double rowValue = x.At(row, col);
                  double rowValueSigma = (rowValue - average) / standardDeviation;

                  gain = Math.Max(gain, Math.Abs(rowValueSigma));
               }
            }

            gains[col] = gain;
         }
         );

         double bestGain = 0.0;
         int bestFeature = -1;

         for (int col = 0; col < samples.ColumnCount; col++)
         {
            double gain = gains[col];

            if (gain > bestGain)
            {
               bestGain = gain;
               bestFeature = col;
            }
         }

         {
            double average = featureProperties.Average[bestFeature];

            bestSegments = new Range[] { new Range(double.MinValue, average), new Range(average, double.MaxValue) };
         }

         return new Tuple<int, double, Range[]>(bestFeature, bestGain, bestSegments);
      }

      /// <summary>Gets best split.</summary>
      /// <param name="samples">The Matrix to process.</param>
      /// <param name="y">The Vector to process.</param>
      /// <param name="used">The used.</param>
      /// <returns>The best split.</returns>
      private Tuple<int, double, Range[]> GetBestSplit(Matrix samples, Matrix x)
      {
         Range[] bestSegments = null;

         Summary featureProperties = new Summary()
         {
            Average = samples.Mean(VectorType.Row),
            StandardDeviation = samples.StdDev(VectorType.Row),
            Minimum = samples.Min(VectorType.Row),
            Maximum = samples.Max(VectorType.Row),
         };

         double[] gains = new double[samples.Cols];

         Parallel.For(0, samples.Cols, col =>
         //for (int col = 0; col < samples.Cols; col++)
         {
            double gain = 0;
            //Range[] segments = null;

            double average = featureProperties.Average[col];
            double standardDeviation = featureProperties.StandardDeviation[col];
            //double minimumValue = featureProperties.Minimum[i];
            //double maximumValue = featureProperties.Maximum[i];

            if (standardDeviation > double.Epsilon)
            {
               //double minimumValueSigma = minimumValue - average / standardDeviation;
               //double maximumValueSigma = maximumValue - average / standardDeviation;

               for (int row = 0; row < x.Rows; row++)
               {
                  double rowValue = x[row][col];
                  double rowValueSigma = (rowValue - average) / standardDeviation;

                  gain = Math.Max(gain, Math.Abs(rowValueSigma));
                  //segments = new Range[] { new Range(double.MinValue, average), new Range(average, double.MaxValue) };
               }
            }

            gains[col] = gain;

            // best one?
            //if (gain > bestGain)
            //{
            //   bestGain = gain;
            //   bestFeature = col;
            //   bestSegments = segments;
            //}
         }
         );

         double bestGain = 0.0;
         int bestFeature = -1;

         for (int col = 0; col < samples.Cols; col++)
         {
            double gain = gains[col];

            if (gain > bestGain)
            {
               bestGain = gain;
               bestFeature = col;
            }
         }

         {
            double average = featureProperties.Average[bestFeature];

            bestSegments = new Range[] { new Range(double.MinValue, average), new Range(average, double.MaxValue) };
         }

         return new Tuple<int, double, Range[]>(bestFeature, bestGain, bestSegments);
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

   public interface IDataProvider
   {

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
