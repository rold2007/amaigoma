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
   using System.Threading;
   using System.Threading.Tasks;

   public class PakiraGenerator : DecisionTreeGenerator
   {
      static public int UNKNOWN_CLASS_INDEX = -1;
      static public int INSUFFICIENT_SAMPLES_CLASS_INDEX = -2;

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

            IEnumerable<IEnumerable<double>> doubles = Descriptor.Convert(value, false);

            convertedSamples = doubles.ToMatrix();
         }
      }

      public int MinimumSampleCount { get; set; }

      /// <summary>Generate model based on a set of examples.</summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="examples">Example set.</param>
      /// <returns>Model.</returns>
      public new PakiraModel Generate(IEnumerable<object> examples)
      {
         if (examples.Count() == 0) throw new InvalidOperationException("Empty example set.");

         if (Descriptor == null)
            throw new InvalidOperationException("Descriptor is null");

         return Generate(Descriptor, examples) as PakiraModel;
      }

      public override IModel Generate(Matrix x, Vector y)
      {
         if (Descriptor == null)
         {
            throw new InvalidOperationException("Cannot build decision tree without type knowledge!");
         }

         StringProperty labelsProperty = Descriptor.Label as StringProperty;
         List<string> labelsList = new List<string>(labelsProperty.Dictionary)
         {
            "Insufficient",
            "Unknown"
         };

         labelsProperty.Dictionary = labelsList.ToArray();

         if (Hint != UNKNOWN_CLASS_INDEX)
         {
            const string errorMessage = "Default class does not exists in descriptors.";

            Should.NotThrow(() =>
            {
               object convertedHint = Descriptor.Label.Convert(Hint);

               convertedHint.ShouldNotBeNull(errorMessage);
            }, errorMessage
            );
         }

         this.Preprocess(x);

         Tree tree = new Tree();

         tree.Root = BuildTree(convertedSamples, x, y, Depth, tree);

         return new PakiraModel
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
         int labelsCount = (Descriptor.Label as StringProperty).Dictionary.Length;

         if (depth < 0)
         {
            if (Hint < 0)
            {
               // We already reached the maximum allowed depth. Create an indecisive node at -1
               return BuildLeafNode(labelsCount + Hint);
            }
            else
            {
               return BuildLeafNode(Hint);
            }
         }

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
                  Node child = BuildLeafNode(Hint < 0 ? labelsCount + Hint : Hint);

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
            System.Diagnostics.Debug.Assert(false, "Need to debug this and see if I want to do anything special here.");

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

            // get appropriate feature at index i
            // (important on because of multivalued
            // cols)
            Property property = Descriptor.At(col);

            // if discrete, calculate full relative gain
            if (property.Discrete)
            {
               System.Diagnostics.Debug.Assert(false, "Need to debug this and see if I want to do anything special here.");
            }
            // otherwise segment based on width
            else
            {
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
}