using Amaigoma;
using Shouldly;
using System;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace AmaigomaTests
{
   public record PakiraGeneratorTests // ncrunch: no coverage
   {
      private readonly PakiraDecisionTreeGenerator pakiraDecisionTreeGenerator;

      public PakiraGeneratorTests()
      {
         pakiraDecisionTreeGenerator = new();

         Console.WriteLine("PakiraDecisionTreeGenerator random seed: " + pakiraDecisionTreeGenerator.randomSeed.ToString());
      }

      internal sealed record MeanDistanceDataTransformer
      {
         readonly ImmutableList<ImmutableList<int>> DataSamples;

         public MeanDistanceDataTransformer(ImmutableList<ImmutableList<int>> dataSamples)
         {
            DataSamples = dataSamples;
         }

         public int ConvertAll(int id, int featureIndex)
         {
            int add = DataSamples[id][featureIndex] + DataSamples[id][featureIndex + 1];

            add = 255 - add;
            add = Math.Abs(add);

            return add;
         }

         public int FeaturesCount => DataSamples[0].Count - 1;
      }

      [Fact]
      public void Generate()
      {
         ImmutableList<ImmutableList<int>> data = [];
         ImmutableList<int> labels = [];

         data = data.Add([2, 90]);
         data = data.Add([250, 140]);
         data = data.Add([200, 100]);

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiETL tanukiETL = new(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new(pakiraDecisionTreeModel.Tree, tanukiETL);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.First().ShouldBe(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.First().ShouldBe(labels[1]);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.Count().ShouldBe(1);
      }

      [Fact]
      public void GenerateMultipleCalls()
      {
         ImmutableList<ImmutableList<int>> data = [];
         ImmutableList<int> labels = [];

         data = data.Add([2, 90]);
         data = data.Add([250, 140]);
         data = data.Add([200, 100]);

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiETL tanukiETL = new(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new(pakiraDecisionTreeModel.Tree, tanukiETL);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.First().ShouldBe(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.First().ShouldBe(labels[1]);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.Count().ShouldBe(1);

         data = data.Add([3, 91]);
         data = data.Add([128, 95]);
         data = data.Add([201, 101]);
         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiETL tanukiETL2 = new(data, labels);

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(3, 3), tanukiETL2);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.First().ShouldBe(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.First().ShouldBe(labels[1]);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.Count().ShouldBe(1);

         PakiraTreeWalker pakiraTreeWalker2 = new(pakiraDecisionTreeModel.Tree, tanukiETL2);

         pakiraTreeWalker2.PredictLeaf(0).LabelValues.First().ShouldBe(labels[0]);
         pakiraTreeWalker2.PredictLeaf(1).LabelValues.First().ShouldBe(labels[1]);
         pakiraTreeWalker2.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
         pakiraTreeWalker2.PredictLeaf(3).LabelValues.First().ShouldBe(labels[3]);
         pakiraTreeWalker2.PredictLeaf(4).LabelValues.First().ShouldBe(labels[4]);
         pakiraTreeWalker2.PredictLeaf(5).LabelValues.First().ShouldBe(labels[5]);
         pakiraTreeWalker2.PredictLeaf(0).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker2.PredictLeaf(1).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker2.PredictLeaf(2).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker2.PredictLeaf(3).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker2.PredictLeaf(4).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker2.PredictLeaf(5).LabelValues.Count().ShouldBe(1);
      }

      [Fact]
      public void DataTransformers()
      {
         ImmutableList<ImmutableList<int>> data = [];
         ImmutableList<int> labels = [];

         data = data.Add([2, 3]);
         data = data.Add([120, 140]);
         data = data.Add([190, 200]);

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         PassThroughTransformer passThroughTransformer = new(data);
         MeanDistanceDataTransformer meanDistanceDataTransformer = new(data);

         Func<int, int, int> dataTransformers = (int id, int featureIndex) =>
         {
            if (featureIndex <= 1)
            {
               return passThroughTransformer.ConvertAll(id, featureIndex);
            }
            else
            {
               featureIndex.ShouldBe(2);

               return meanDistanceDataTransformer.ConvertAll(id, featureIndex - 2);
            }
         };

         TanukiETL tanukiETL = new(dataTransformers, new PassThroughLabelsTransformer(labels).ConvertAll, passThroughTransformer.FeaturesCount + meanDistanceDataTransformer.FeaturesCount);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new(pakiraDecisionTreeModel.Tree, tanukiETL);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.First().ShouldBe(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.First().ShouldBe(labels[1]);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.Count().ShouldBe(1);

         // The data transformers should allow to produce a very shallow tree
         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBe(1);
      }

      [Fact]
      public void DataTransformersQuickExit()
      {
         ImmutableList<ImmutableList<int>> data = [];
         ImmutableList<int> labels = [];

         data = data.Add([25, 35]);
         data = data.Add([120, 140]);
         data = data.Add([190, 200]);

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         PassThroughTransformer passThroughTransformer = new(data);
         MeanDistanceDataTransformer meanDistanceDataTransformer = new(data);

         Func<int, int, int> dataTransformers = (int id, int featureIndex) =>
         {
            if (featureIndex == 0)
            {
               return meanDistanceDataTransformer.ConvertAll(id, 0);
            }
            else
            {
               return passThroughTransformer.ConvertAll(id, (featureIndex - 1) % 2);
            }
         };

         TanukiETL tanukiETL = new(dataTransformers, new PassThroughLabelsTransformer(labels).ConvertAll, meanDistanceDataTransformer.FeaturesCount + passThroughTransformer.FeaturesCount * 100);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new(pakiraDecisionTreeModel.Tree, tanukiETL);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.First().ShouldBe(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.First().ShouldBe(labels[1]);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.Count().ShouldBe(1);

         // The data transformers should allow to produce a very shallow tree
         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBeInRange(1, 5);
      }

      [Fact]
      public void DataTransformersQuickExit2()
      {
         ImmutableList<ImmutableList<int>> data = [];
         ImmutableList<int> labels = [];

         data = data.Add([25, 35]);
         data = data.Add([120, 140]);
         data = data.Add([190, 200]);

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         MeanDistanceDataTransformer meanDistanceDataTransformer = new(data);

         TanukiETL tanukiETL = new(meanDistanceDataTransformer.ConvertAll, new PassThroughLabelsTransformer(labels).ConvertAll, meanDistanceDataTransformer.FeaturesCount);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new(pakiraDecisionTreeModel.Tree, tanukiETL);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.First().ShouldBe(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.First().ShouldBe(labels[1]);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.Count().ShouldBe(1);

         // The data transformers should allow to produce a very shallow tree
         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBe(1);
      }

      [Fact]
      public void DeepTree()
      {
         ImmutableList<ImmutableList<int>> data = [];
         ImmutableList<int> labels = [];

         data = data.Add([2, 3]);
         data = data.Add([250, 254]);
         data = data.Add([250, 255]);
         data = data.Add([251, 253]);
         data = data.Add([251, 254]);
         data = data.Add([1, 2]);
         data = data.Add([2, 1]);
         data = data.Add([2, 2]);
         data = data.Add([3, 2]);

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);
         labels = labels.Add(6);
         labels = labels.Add(9);
         labels = labels.Add(96);
         labels = labels.Add(97);
         labels = labels.Add(98);
         labels = labels.Add(99);

         TanukiETL tanukiETL = new(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new(pakiraDecisionTreeModel.Tree, tanukiETL);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.First().ShouldBe(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.First().ShouldBe(labels[1]);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.Count().ShouldBe(1);

         for (int i = 0; i < data.Count; i++)
         {
            pakiraTreeWalker.PredictLeaf(i).LabelValues.First().ShouldBe(labels[i], "i=" + i.ToString());
            pakiraTreeWalker.PredictLeaf(i).LabelValues.Count().ShouldBe(1);
         }
      }

      [Fact]
      public void CertaintyScore()
      {
         ImmutableList<ImmutableList<int>> data = [];
         ImmutableList<int> labels = [];

         data = data.Add([2, 90]);
         data = data.Add([250, 140]);
         data = data.Add([200, 100]);

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiETL tanukiETL = new(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new(pakiraDecisionTreeModel.Tree, tanukiETL);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.First().ShouldBe(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.First().ShouldBe(labels[1]);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.Count().ShouldBe(1);
      }

      [Fact]
      public void GenerateCannotSplit()
      {
         ImmutableList<ImmutableList<int>> data = [];
         ImmutableList<int> labels = [];

         data = data.Add([2, 90]);
         data = data.Add([250, 140]);
         data = data.Add([250, 140]);

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiETL tanukiETL = new(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new(pakiraDecisionTreeModel.Tree, tanukiETL);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.First().ShouldBe(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(2);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.Count().ShouldBe(2);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.ShouldContain(labels[1]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.ShouldContain(labels[2]);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.ShouldContain(labels[1]);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.ShouldContain(labels[2]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(1);
      }

      [Fact]
      public void GenerateCannotSplit2()
      {
         ImmutableList<ImmutableList<int>> data = [];
         ImmutableList<int> labels = [];

         data = data.Add([250, 140]);
         data = data.Add([250, 140]);

         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiETL tanukiETL = new(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new(pakiraDecisionTreeModel.Tree, tanukiETL);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(2);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(2);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.ShouldContain(labels[0]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.ShouldContain(labels[1]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.ShouldContain(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.ShouldContain(labels[1]);
      }

      [Fact]
      public void CallPakiraTreeReplaceLeaf()
      {
         ImmutableList<ImmutableList<int>> data = [];
         ImmutableList<int> labels = [];

         data = data.Add([2]);
         data = data.Add([2]);

         labels = labels.Add(42);
         labels = labels.Add(54);

         TanukiETL tanukiETL = new(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         data = data.Add([215]);

         labels = labels.Add(42);

         TanukiETL tanukiETL2 = new(data, labels);

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(2, 1), tanukiETL2);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new(pakiraDecisionTreeModel.Tree, tanukiETL);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(2);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(2);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.ShouldContain(labels[0]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.ShouldContain(labels[1]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.ShouldContain(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.ShouldContain(labels[1]);

         PakiraTreeWalker pakiraTreeWalker2 = new(pakiraDecisionTreeModel.Tree, tanukiETL2);

         pakiraTreeWalker2.PredictLeaf(2).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker2.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
      }
   }
}
