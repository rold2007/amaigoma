namespace Amaigoma
{
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
   using System.Linq;

   public class PakiraGenerator : DecisionTreeGenerator
   {
      static public double UNKNOWN_CLASS_INDEX = -1.0;
      static public double INSUFFICIENT_SAMPLES_CLASS_INDEX = -2.0;

      public PakiraGenerator()
      {
      }

      public PakiraGenerator(Descriptor descriptor, IEnumerable<object> samples, double defaultClassIndex, int minimumSampleCount) : this()
      {
         Descriptor = descriptor;
         Samples = samples;
         Hint = defaultClassIndex;
         ImpurityType = typeof(DispersionError);
         Depth = int.MaxValue;
         MinimumSampleCount = minimumSampleCount;
      }

      private IEnumerable<object> samples;
      private Matrix convertedSamples;

      public IEnumerable<object> Samples
      {
         get
         {
            return samples;
         }

         set
         {
            samples = value;

            var doubles = Descriptor.Convert(value, false, false);

            convertedSamples = doubles.ToMatrix();
         }
      }

      public int MinimumSampleCount { get; set; }

      public override IModel Generate(Matrix x, Vector y)
      {
         Need to update the descriptor labels to add -1 and - 2, but they will need to be positive...

         if (Descriptor == null)
         {
            throw new InvalidOperationException("Cannot build decision tree without type knowledge!");
         }

         if (Hint != UNKNOWN_CLASS_INDEX)
         {
            const string errorMessage = "Default class doesn not exists in descriptors.";

            Should.NotThrow(() =>
            {
               object convertedHint = Descriptor.Label.Convert(Hint);

               convertedHint.ShouldNotBeNull(errorMessage);
            }, errorMessage
            );
         }

         this.Preprocess(x);

         var tree = new Tree();

         tree.Root = BuildTree(convertedSamples, x, y, Depth, tree);

         return new DecisionTreeModel
         {
            Descriptor = Descriptor,
            NormalizeFeatures = NormalizeFeatures,
            FeatureNormalizer = FeatureNormalizer,
            FeatureProperties = FeatureProperties,
            Tree = tree,
            Hint = Hint
         };
      }

      /// <summary>Builds a tree.</summary>
      /// <param name="x">The Matrix to process.</param>
      /// <param name="y">The Vector to process.</param>
      /// <param name="depth">The depth.</param>
      /// <returns>A Node.</returns>
      private Node BuildTree(Matrix samples, Matrix x, Vector y, int depth, Tree tree)
      {
         if (depth < 0)
         {
            // We already reached the maximum allowed depth. Create an indecisive node at -1
            return BuildLeafNode(Hint);
         }

         Tuple<int, double, Range[]> tuple = GetBestSplit(samples);
         int col = tuple.Item1;
         double gain = tuple.Item2;
         Range[] segments = tuple.Item3;

         // Cannot find a split in the samples.
         // Not enough samples or all samples are identical.
         if (col == -1)
         {
            return BuildLeafNode(INSUFFICIENT_SAMPLES_CLASS_INDEX);
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
               System.Diagnostics.Debug.Assert(false, "Need to debug this and see if I want to do anything special here.");

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
                     int nextDepth = depth == int.MaxValue ? depth : depth - 1;

                     Node child = BuildTree(samples.Slice(samplesSlice), x.Slice(slice), ySlice, nextDepth, tree);

                     tree.AddVertex(child);
                     edge.ChildId = child.Id;
                  }

                  edges.Add(edge);
               }
               else
               {
                  // We don't have any training data for this node
                  Node child = BuildLeafNode(Hint);

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
            System.Diagnostics.Debug.Assert(false, "Need to debug this and see if I want to do anything special here.");

            var val = y.Mode();
            node.IsLeaf = true;
            node.Value = val;
         }

         tree.AddVertex(node);

         if (edges.Count > 1)
         {
            foreach (var e in edges)
            {
               tree.AddEdge(e);
            }
         }

         return node;
      }

      /// <summary>Gets best split.</summary>
      /// <param name="x">The Matrix to process.</param>
      /// <param name="y">The Vector to process.</param>
      /// <param name="used">The used.</param>
      /// <returns>The best split.</returns>
      private Tuple<int, double, Range[]> GetBestSplit(Matrix x)
      {
         double bestGain = 0.0;
         int bestFeature = -1;

         Range[] bestSegments = null;

         Summary featureProperties = new Summary()
         {
            Average = x.Mean(VectorType.Row),
            StandardDeviation = x.StdDev(VectorType.Row),
            Minimum = x.Min(VectorType.Row),
            Maximum = x.Max(VectorType.Row),
         };

         for (int i = 0; i < x.Cols; i++)
         {
            double gain = 0;
            Range[] segments = null;

            // get appropriate feature at index i
            // (important on because of multivalued
            // cols)
            var property = Descriptor.At(i);

            // if discrete, calculate full relative gain
            if (property.Discrete)
            {
               System.Diagnostics.Debug.Assert(false, "Need to debug this and see if I want to do anything special here.");
            }
            // otherwise segment based on width
            else
            {
               double average = featureProperties.Average[i];
               double standardDeviation = featureProperties.StandardDeviation[i];
               double minimumValue = featureProperties.Minimum[i];
               double maximumValue = featureProperties.Maximum[i];

               if (standardDeviation > double.Epsilon)
               {
                  double minimumValueSigma = minimumValue - average / standardDeviation;
                  double maximumValueSigma = maximumValue - average / standardDeviation;

                  gain = Math.Max(Math.Abs(minimumValueSigma), Math.Abs(maximumValueSigma));
                  segments = new Range[] { new Range(double.MinValue, average), new Range(average, double.MaxValue) };
               }
            }

            // best one?
            if (gain > bestGain)
            {
               bestGain = gain;
               bestFeature = i;
               bestSegments = segments;
            }
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
}