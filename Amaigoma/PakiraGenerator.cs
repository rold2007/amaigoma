namespace Amaigoma
{
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using numl.Data;
   using numl.Math;
   using numl.Math.Information;
   using numl.Math.LinearAlgebra;
   using numl.Model;
   using numl.Supervised;
   using numl.Supervised.DecisionTree;
   using numl.Utils;

   public class PakiraGenerator : DecisionTreeGenerator
   {
      public PakiraGenerator()
      {
      }

      public PakiraGenerator(Descriptor descriptor, IEnumerable<object> samples, double defaultClassIndex) : this()
      {
         Descriptor = descriptor;
         Samples = samples;
         Hint = defaultClassIndex;
         ImpurityType = typeof(DispersionError);
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

      public override IModel Generate(Matrix x, Vector y)
      {
         if (Descriptor == null)
         {
            throw new InvalidOperationException("Cannot build decision tree without type knowledge!");
         }

         System.Diagnostics.Debug.Assert(Descriptor.Label.Convert(Hint) != null, "Default class does not exists.");

         this.Preprocess(x);

         var tree = new Tree();

         tree.Root = BuildTree(convertedSamples, y, Depth, tree);

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
      private Node BuildTree(Matrix x, Vector y, int depth, Tree tree)
      {
         if (depth < 0)
         {
            System.Diagnostics.Debug.Assert(false, "Need to debug this and see if I want to do anything special here.");

            return BuildLeafNode(y.Mode());
         }

         var tuple = GetBestSplit(x, y);
         var col = tuple.Item1;
         var gain = tuple.Item2;
         var segments = tuple.Item3;

         // uh oh, need to return something?
         // a weird node of some sort...
         // but just in case...
         if (col == -1)
         {
            System.Diagnostics.Debug.Assert(false, "Need to debug this and see if I want to do anything special here.");

            return BuildLeafNode(y.Mode());
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
            var segment = segments[i];
            var edge = new Edge()
            {
               ParentId = node.Id,
               Discrete = false, // measure.Discrete,
               Min = segment.Min,
               Max = segment.Max
            };

            IEnumerable<int> slice;

            if (edge.Discrete)
            {
               System.Diagnostics.Debug.Assert(false, "Need to debug this and see if I want to do anything special here.");

               // get discrete label
               edge.Label = Descriptor.At(col).Convert(segment.Min).ToString();
               // do value check for matrix slicing
               slice = x.Indices(v => v[col] == segment.Min);
            }
            else
            {
               // get range label
               edge.Label = string.Format("{0} <= x < {1}", segment.Min, segment.Max);
               // do range check for matrix slicing
               slice = x.Indices(v => v[col] >= segment.Min && v[col] < segment.Max);
            }

            // something to look at?
            // if this number is 0 then this edge 
            // leads to a dead end - the edge will 
            // not be built
            if (slice.Count() > 0)
            {
               Vector ySlice = y.Slice(slice);
               // only one answer, set leaf
               if (ySlice.Distinct().Count() == 1)
               {
                  var child = BuildLeafNode(ySlice[0]);
                  tree.AddVertex(child);
                  edge.ChildId = child.Id;
               }
               // otherwise continue to build tree
               else
               {
                  var child = BuildTree(x.Slice(slice), ySlice, depth - 1, tree);
                  tree.AddVertex(child);
                  edge.ChildId = child.Id;
               }

               edges.Add(edge);
            }
            else
            {
               System.Diagnostics.Debug.Assert(false, "Need to debug this and see if I want to do anything special here.");
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
      private Tuple<int, double, Range[]> GetBestSplit(Matrix x, Vector y)
      {
         double bestGain = -1;
         int bestFeature = -1;

         Range[] bestSegments = null;

         numl.Math.Summary featureProperties = new numl.Math.Summary()
         {
            Average = x.Mean(VectorType.Row),
            StandardDeviation = x.StdDev(VectorType.Row),
            Minimum = x.Min(VectorType.Row),
            Maximum = x.Max(VectorType.Row),
            //Median = x.Median(VectorType.Row)
         };

         //for (int i = 0; i < x.Cols; i++)
         for (int i = 0; i < x.Cols; i++)
         {
            double gain = 0;
            Range[] segments = null;

            //System.Diagnostics.Debug.Assert(false, "Need to create my own impurity logic.");

            //Impurity measure = (Impurity)Ject.Create(ImpurityType);

            //Vector convertedSamplesColumn = convertedSamples[i];
            //double average = convertedSamplesColumn.Average();


            // get appropriate column vector
            //var feature = x.Col(i);
            // get appropriate feature at index i
            // (important on because of multivalued
            // cols)
            var property = Descriptor.At(i);
            // if discrete, calculate full relative gain
            if (property.Discrete)
            {
               System.Diagnostics.Debug.Assert(false, "Need to debug this and see if I want to do anything special here.");
               //gain = measure.RelativeGain(y, feature);
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
                  segments = new Range[] {new Range(double.MinValue, average), new Range(average, double.MaxValue) };
               }
               //gain = measure.SegmentedRelativeGain(y, feature, Width);
            }

            // best one?
            if (gain > bestGain)
            {
               bestGain = gain;
               bestFeature = i;
               //bestMeasure = measure;
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