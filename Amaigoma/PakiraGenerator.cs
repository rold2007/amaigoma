namespace Amaigoma
{
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using numl.Data;
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

      public PakiraGenerator(Descriptor descriptor, IEnumerable<object> samples) : this()
      {
         Descriptor = descriptor;
         Samples = samples;
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

         //use this on the random sample
         //this.FeatureProperties = new numl.Math.Summary()
         //{
         //   Average = X.Mean(VectorType.Row),
         //       StandardDeviation = X.StdDev(VectorType.Row),
         //       Minimum = X.Min(VectorType.Row),
         //       Maximum = X.Max(VectorType.Row),
         //       Median = X.Median(VectorType.Row)
         //   };


         if (Descriptor == null)
         {
            throw new InvalidOperationException("Cannot build decision tree without type knowledge!");
         }

         this.Preprocess(x);

         var tree = new Tree();

         tree.Root = BuildTree(x, y, Depth, new List<int>(x.Cols), tree);

         // have to guess something....
         // especially when automating
         // the thing in a Learner
         // this only happens if it is something
         // it has never seen.
         if (Hint == double.Epsilon)
            Hint = y.GetRandom(); // flip a coin...

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
      /// <param name="used">The used.</param>
      /// <returns>A Node.</returns>
      private Node BuildTree(Matrix x, Vector y, int depth, List<int> used, Tree tree)
      {
         if (depth < 0)
            return BuildLeafNode(y.Mode());

         var tuple = GetBestSplit(x, y, used);
         var col = tuple.Item1;
         var gain = tuple.Item2;
         var measure = tuple.Item3;

         // uh oh, need to return something?
         // a weird node of some sort...
         // but just in case...
         if (col == -1)
            return BuildLeafNode(y.Mode());

         used.Add(col);

         Node node = new Node
         {
            Column = col,
            Gain = gain,
            IsLeaf = false,
            Name = Descriptor.ColumnAt(col)
         };

         // populate edges
         List<Edge> edges = new List<Edge>(measure.Segments.Length);
         for (int i = 0; i < measure.Segments.Length; i++)
         {
            // working set
            var segment = measure.Segments[i];
            var edge = new Edge()
            {
               ParentId = node.Id,
               Discrete = measure.Discrete,
               Min = segment.Min,
               Max = segment.Max
            };

            IEnumerable<int> slice;

            if (edge.Discrete)
            {
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
                  var child = BuildTree(x.Slice(slice), ySlice, depth - 1, used, tree);
                  tree.AddVertex(child);
                  edge.ChildId = child.Id;
               }

               edges.Add(edge);
            }
         }

         // problem, need to convert
         // parent to terminal node
         // with mode
         if (edges.Count <= 1)
         {
            var val = y.Mode();
            node.IsLeaf = true;
            node.Value = val;
         }

         tree.AddVertex(node);

         if (edges.Count > 1)
            foreach (var e in edges)
               tree.AddEdge(e);

         return node;
      }

      /// <summary>Gets best split.</summary>
      /// <param name="x">The Matrix to process.</param>
      /// <param name="y">The Vector to process.</param>
      /// <param name="used">The used.</param>
      /// <returns>The best split.</returns>
      private Tuple<int, double, Impurity> GetBestSplit(Matrix x, Vector y, List<int> used)
      {
         double bestGain = -1;
         int bestFeature = -1;

         Impurity bestMeasure = null;
         for (int i = 0; i < x.Cols; i++)
         {
            // already used?
            if (used.Contains(i)) continue;

            double gain = 0;

            Impurity measure = (Impurity)Ject.Create(ImpurityType);

            // get appropriate column vector
            var feature = x.Col(i);
            // get appropriate feature at index i
            // (important on because of multivalued
            // cols)
            var property = Descriptor.At(i);
            // if discrete, calculate full relative gain
            if (property.Discrete)
               gain = measure.RelativeGain(y, feature);
            // otherwise segment based on width
            else
               gain = measure.SegmentedRelativeGain(y, feature, Width);

            // best one?
            if (gain > bestGain)
            {
               bestGain = gain;
               bestFeature = i;
               bestMeasure = measure;
            }
         }

         return new Tuple<int, double, Impurity>(bestFeature, bestGain, bestMeasure);
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