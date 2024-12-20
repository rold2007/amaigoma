using Amaigoma;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace AmaigomaTests
{
   public record PakiraGeneratorTests // ncrunch: no coverage
   {
      internal record MeanDistanceDataTransformer // ncrunch: no coverage
      {
         public MeanDistanceDataTransformer()
         {
         }

         public static IEnumerable<double> ConvertAll(IEnumerable<double> list)
         {
            ImmutableList<double> result = ImmutableList<double>.Empty;

            for (int i = 0; i < list.Count() - 1; i++)
            {
               double add = list.ElementAt(i) + list.ElementAt(i + 1);

               add = 255 - add;
               add = Math.Abs(add);

               result = result.Add(add);
            }

            return result;
         }
      }

      // UNDONE This should be done automatically upon initialization of each test
      public static PakiraDecisionTreeGenerator CreatePakiraGeneratorInstance()
      {
         PakiraDecisionTreeGenerator pakiraDecisionTreeGenerator = new();

         Console.WriteLine("PakiraDecisionTreeGenerator random seed: " + PakiraDecisionTreeGenerator.randomSeed.ToString());

         return pakiraDecisionTreeGenerator;
      }

      [Fact]
      public void Generate()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add(ImmutableList.CreateRange(new double[] { 2, 90 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 250, 140 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 200, 100 }));

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiTransformers tanukiTransformers = new TanukiTransformers(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiTransformers);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiTransformers);

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
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add(ImmutableList.CreateRange(new double[] { 2, 90 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 250, 140 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 200, 100 }));

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiTransformers tanukiTransformers = new TanukiTransformers(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiTransformers);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiTransformers);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.First().ShouldBe(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.First().ShouldBe(labels[1]);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.Count().ShouldBe(1);

         data = data.Add(ImmutableList.CreateRange(new double[] { 3, 91 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 128, 95 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 201, 101 }));
         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiTransformers tanukiTransformers2 = new TanukiTransformers(data, labels);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(3, 3), tanukiTransformers2);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiTransformers);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.First().ShouldBe(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.First().ShouldBe(labels[1]);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.Count().ShouldBe(1);

         PakiraTreeWalker pakiraTreeWalker2 = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiTransformers2);

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
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add(ImmutableList.CreateRange(new double[] { 2, 3 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 120, 140 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 190, 200 }));

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         PassThroughTransformer passThroughTransformer = new();
         MeanDistanceDataTransformer meanDistanceDataTransformer = new();

         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         dataTransformers += passThroughTransformer.ConvertAll;
         dataTransformers += MeanDistanceDataTransformer.ConvertAll;

         TanukiTransformers tanukiTransformers = new TanukiTransformers(0, new IndexedDataExtractor(data).ConvertAll, dataTransformers, new IndexedLabelExtractor(labels).ConvertAll);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiTransformers);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiTransformers);

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
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add(ImmutableList.CreateRange(new double[] { 25, 35 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 120, 140 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 190, 200 }));

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         PassThroughTransformer passThroughTransformer = new();
         MeanDistanceDataTransformer meanDistanceDataTransformer = new();

         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         dataTransformers += MeanDistanceDataTransformer.ConvertAll;

         for (int i = 0; i < 100; i++)
         {
            dataTransformers += passThroughTransformer.ConvertAll;
         }

         TanukiTransformers tanukiTransformers = new TanukiTransformers(0, new IndexedDataExtractor(data).ConvertAll, dataTransformers, new IndexedLabelExtractor(labels).ConvertAll);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiTransformers);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiTransformers);

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
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add(ImmutableList.CreateRange(new double[] { 25, 35 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 120, 140 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 190, 200 }));

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         MeanDistanceDataTransformer meanDistanceDataTransformer = new();

         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         dataTransformers += MeanDistanceDataTransformer.ConvertAll;

         TanukiTransformers tanukiTransformers = new TanukiTransformers(0, new IndexedDataExtractor(data).ConvertAll, dataTransformers, new IndexedLabelExtractor(labels).ConvertAll);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiTransformers);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiTransformers);

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
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add(ImmutableList.CreateRange(new double[] { 2, 3 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 250, 254 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 250, 255 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 251, 253 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 251, 254 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 1, 2 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 2, 1 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 2, 2 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 3, 2 }));

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);
         labels = labels.Add(6);
         labels = labels.Add(9);
         labels = labels.Add(96);
         labels = labels.Add(97);
         labels = labels.Add(98);
         labels = labels.Add(99);

         TanukiTransformers tanukiTransformers = new TanukiTransformers(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiTransformers);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiTransformers);

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
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add(ImmutableList.CreateRange(new double[] { 2, 90 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 250, 140 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 200, 100 }));

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiTransformers tanukiTransformers = new TanukiTransformers(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiTransformers);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiTransformers);

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
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add(ImmutableList.CreateRange(new double[] { 2, 90 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 250, 140 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 250, 140 }));

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiTransformers tanukiTransformers = new TanukiTransformers(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiTransformers);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiTransformers);

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
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add(ImmutableList.CreateRange(new double[] { 250, 140 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 250, 140 }));

         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiTransformers tanukiTransformers = new TanukiTransformers(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiTransformers);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiTransformers);

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
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add(ImmutableList.CreateRange(new double[] { 2 }));
         data = data.Add(ImmutableList.CreateRange(new double[] { 2 }));

         labels = labels.Add(42);
         labels = labels.Add(54);

         TanukiTransformers tanukiTransformers = new TanukiTransformers(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiTransformers);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         data = data.Add(ImmutableList.CreateRange(new double[] { 215 }));

         labels = labels.Add(42);

         TanukiTransformers tanukiTransformers2 = new TanukiTransformers(data, labels);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(2, 1), tanukiTransformers2);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiTransformers);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(2);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(2);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.ShouldContain(labels[0]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.ShouldContain(labels[1]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.ShouldContain(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.ShouldContain(labels[1]);

         PakiraTreeWalker pakiraTreeWalker2 = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiTransformers2);

         pakiraTreeWalker2.PredictLeaf(2).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker2.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
      }
   }
}
