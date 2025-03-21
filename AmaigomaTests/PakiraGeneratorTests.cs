﻿using Amaigoma;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

// UNDONE Fix all commented code in tests
namespace AmaigomaTests
{
   using DataTransformer = Func<IEnumerable<double>, double>;

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
         ImmutableList<ImmutableList<double>> DataSamples;

         public MeanDistanceDataTransformer(ImmutableList<ImmutableList<double>> dataSamples)
         {
            DataSamples = dataSamples;
         }

         public double ConvertAll(int id, int featureIndex)
         {
            double add = DataSamples[id][featureIndex] + DataSamples[id][featureIndex + 1];

            add = 255 - add;
            add = Math.Abs(add);

            return add;
         }

         public int FeaturesCount => DataSamples[0].Count - 1;
      }

      [Fact]
      public void Generate()
      {
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

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

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL);

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
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add([2, 90]);
         data = data.Add([250, 140]);
         data = data.Add([200, 100]);

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiETL tanukiETL = new TanukiETL(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL);

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

         TanukiETL tanukiETL2 = new TanukiETL(data, labels);

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(3, 3), tanukiETL2);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.First().ShouldBe(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.First().ShouldBe(labels[1]);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker.PredictLeaf(2).LabelValues.Count().ShouldBe(1);

         PakiraTreeWalker pakiraTreeWalker2 = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL2);

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
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add([2, 3]);
         data = data.Add([120, 140]);
         data = data.Add([190, 200]);

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         PassThroughTransformer passThroughTransformer = new(data);
         MeanDistanceDataTransformer meanDistanceDataTransformer = new(data);

         Func<int, int, double> dataTransformers = (int id, int featureIndex) =>
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

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL);

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
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add([25, 35]);
         data = data.Add([120, 140]);
         data = data.Add([190, 200]);

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         // PassThroughTransformer passThroughTransformer = new(data[0].Count);
         // MeanDistanceDataTransformer meanDistanceDataTransformer = new(data[0].Count);

         // ImmutableList<DataTransformer> dataTransformers = ImmutableList<DataTransformer>.Empty;
         // ImmutableList<DataTransformerIndices> dataTransformerIndices = ImmutableList<DataTransformerIndices>.Empty;

         // // TODO Removed a data transformer to be able to run faster until the performances are improved
         // dataTransformers = dataTransformers.AddRange(meanDistanceDataTransformer.DataTransformers);
         // dataTransformerIndices = dataTransformerIndices.AddRange(meanDistanceDataTransformer.DataTransformersIndices);

         // for (int i = 0; i < 100; i++)
         // {
         //    dataTransformers = dataTransformers.AddRange(passThroughTransformer.DataTransformers);
         //    dataTransformerIndices = dataTransformerIndices.AddRange(passThroughTransformer.DataTransformersIndices);
         // }

         // TanukiETL tanukiETL = new TanukiETL(new IndexedDataExtractor(data).ConvertAll, dataTransformers, dataTransformerIndices, new IndexedLabelExtractor(labels).ConvertAll);
         // PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         // pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);
         // pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         // PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL);

         // pakiraTreeWalker.PredictLeaf(0).LabelValues.First().ShouldBe(labels[0]);
         // pakiraTreeWalker.PredictLeaf(1).LabelValues.First().ShouldBe(labels[1]);
         // pakiraTreeWalker.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
         // pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(1);
         // pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(1);
         // pakiraTreeWalker.PredictLeaf(2).LabelValues.Count().ShouldBe(1);

         // // The data transformers should allow to produce a very shallow tree
         // pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBeInRange(1, 5);
      }

      [Fact]
      public void DataTransformersQuickExit2()
      {
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add([25, 35]);
         data = data.Add([120, 140]);
         data = data.Add([190, 200]);

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         // MeanDistanceDataTransformer meanDistanceDataTransformer = new(data[0].Count);
         // ImmutableList<DataTransformer> dataTransformers = ImmutableList<DataTransformer>.Empty;
         // ImmutableList<DataTransformerIndices> dataTransformerIndices = ImmutableList<DataTransformerIndices>.Empty;

         // dataTransformers = dataTransformers.AddRange(meanDistanceDataTransformer.DataTransformers);
         // dataTransformerIndices = dataTransformerIndices.AddRange(meanDistanceDataTransformer.DataTransformersIndices);

         // TanukiETL tanukiETL = new TanukiETL(new IndexedDataExtractor(data).ConvertAll, dataTransformers, dataTransformerIndices, new IndexedLabelExtractor(labels).ConvertAll);
         // PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         // pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);
         // pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         // PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL);

         // pakiraTreeWalker.PredictLeaf(0).LabelValues.First().ShouldBe(labels[0]);
         // pakiraTreeWalker.PredictLeaf(1).LabelValues.First().ShouldBe(labels[1]);
         // pakiraTreeWalker.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
         // pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(1);
         // pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(1);
         // pakiraTreeWalker.PredictLeaf(2).LabelValues.Count().ShouldBe(1);

         // // The data transformers should allow to produce a very shallow tree
         // pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBe(1);
      }

      [Fact]
      public void DeepTree()
      {
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

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

         TanukiETL tanukiETL = new TanukiETL(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL);

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
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add([2, 90]);
         data = data.Add([250, 140]);
         data = data.Add([200, 100]);

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiETL tanukiETL = new TanukiETL(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL);

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
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add([2, 90]);
         data = data.Add([250, 140]);
         data = data.Add([250, 140]);

         labels = labels.Add(42);
         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiETL tanukiETL = new TanukiETL(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL);

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
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add([250, 140]);
         data = data.Add([250, 140]);

         labels = labels.Add(54);
         labels = labels.Add(42);

         TanukiETL tanukiETL = new TanukiETL(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL);

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
         ImmutableList<ImmutableList<double>> data = ImmutableList<ImmutableList<double>>.Empty;
         ImmutableList<int> labels = ImmutableList<int>.Empty;

         data = data.Add([2]);
         data = data.Add([2]);

         labels = labels.Add(42);
         labels = labels.Add(54);

         TanukiETL tanukiETL = new TanukiETL(data, labels);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(0, data.Count), tanukiETL);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         data = data.Add(ImmutableList.CreateRange(new double[] { 215 }));

         labels = labels.Add(42);

         TanukiETL tanukiETL2 = new TanukiETL(data, labels);

         pakiraDecisionTreeModel = pakiraDecisionTreeGenerator.Generate(pakiraDecisionTreeModel, Enumerable.Range(2, 1), tanukiETL2);
         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL);

         pakiraTreeWalker.PredictLeaf(0).LabelValues.Count().ShouldBe(2);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.Count().ShouldBe(2);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.ShouldContain(labels[0]);
         pakiraTreeWalker.PredictLeaf(0).LabelValues.ShouldContain(labels[1]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.ShouldContain(labels[0]);
         pakiraTreeWalker.PredictLeaf(1).LabelValues.ShouldContain(labels[1]);

         PakiraTreeWalker pakiraTreeWalker2 = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL2);

         pakiraTreeWalker2.PredictLeaf(2).LabelValues.Count().ShouldBe(1);
         pakiraTreeWalker2.PredictLeaf(2).LabelValues.First().ShouldBe(labels[2]);
      }
   }
}
