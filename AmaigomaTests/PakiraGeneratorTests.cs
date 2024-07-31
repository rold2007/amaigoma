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

         TrainDataCache trainDataCache = new TrainDataCache();

         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 2, 90 }), 42, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 54, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 200, 100 }), 42, Guid.NewGuid());

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainDataCache.Samples[0].Data);

         trainDataCache = pakiraDecisionTreeModel.PrefetchAll(trainDataCache);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainDataCache);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0].Data).LabelValue.ShouldBe(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValue.ShouldBe(trainDataCache.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[2].Data).LabelValue.ShouldBe(trainDataCache.Labels[2]);

         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0]).PakiraLeaf.LabelValue.ShouldBe(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1]).PakiraLeaf.LabelValue.ShouldBe(trainDataCache.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[2]).PakiraLeaf.LabelValue.ShouldBe(trainDataCache.Labels[2]);
      }

      [Fact]
      public void GenerateMultipleCalls()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();

         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 2, 90 }), 42, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 54, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 200, 100 }), 42, Guid.NewGuid());

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainDataCache.Samples[0].Data);

         trainDataCache = pakiraDecisionTreeModel.PrefetchAll(trainDataCache);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainDataCache);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         TrainDataCache trainDataCache2 = new();

         trainDataCache2 = trainDataCache2.AddSample(ImmutableList.CreateRange(new double[] { 3, 91 }), 42, Guid.NewGuid());
         trainDataCache2 = trainDataCache2.AddSample(ImmutableList.CreateRange(new double[] { 128, 95 }), 54, Guid.NewGuid());
         trainDataCache2 = trainDataCache2.AddSample(ImmutableList.CreateRange(new double[] { 201, 101 }), 42, Guid.NewGuid());

         trainDataCache2 = pakiraDecisionTreeModel.PrefetchAll(trainDataCache2);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainDataCache2);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0].Data).LabelValue.ShouldBe(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValue.ShouldBe(trainDataCache.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[2].Data).LabelValue.ShouldBe(trainDataCache.Labels[2]);

         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0]).PakiraLeaf.LabelValue.ShouldBe(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1]).PakiraLeaf.LabelValue.ShouldBe(trainDataCache.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[2]).PakiraLeaf.LabelValue.ShouldBe(trainDataCache.Labels[2]);

         pakiraDecisionTreeModel.PredictLeaf(trainDataCache2.Samples[0].Data).LabelValue.ShouldBe(trainDataCache2.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache2.Samples[1].Data).LabelValue.ShouldBe(trainDataCache2.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache2.Samples[2].Data).LabelValue.ShouldBe(trainDataCache2.Labels[2]);

         pakiraDecisionTreeModel.PredictLeaf(trainDataCache2.Samples[0]).PakiraLeaf.LabelValue.ShouldBe(trainDataCache2.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache2.Samples[1]).PakiraLeaf.LabelValue.ShouldBe(trainDataCache2.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache2.Samples[2]).PakiraLeaf.LabelValue.ShouldBe(trainDataCache2.Labels[2]);
      }

      [Fact]
      public void MinimumSampleCount()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 2, 3 }), 42, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 20, 140 }), 54, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 33, 200 }), 42, Guid.NewGuid());

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainDataCache.Samples[0].Data);

         trainDataCache = pakiraDecisionTreeModel.PrefetchAll(trainDataCache);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainDataCache);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0].Data).LabelValue.ShouldBe(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValue.ShouldBe(trainDataCache.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[2].Data).LabelValue.ShouldBe(trainDataCache.Labels[2]);
      }

      [Fact]
      public void DataTransformers()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 2, 3 }), 42, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 120, 140 }), 54, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 190, 200 }), 42, Guid.NewGuid());

         PassThroughTransformer passThroughTransformer = new();
         MeanDistanceDataTransformer meanDistanceDataTransformer = new();

         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         dataTransformers += passThroughTransformer.ConvertAll;
         dataTransformers += MeanDistanceDataTransformer.ConvertAll;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(new TanukiTransformers(dataTransformers, trainDataCache.Samples[0].Data));

         trainDataCache = pakiraDecisionTreeModel.PrefetchAll(trainDataCache);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainDataCache);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0].Data).LabelValue.ShouldBe(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValue.ShouldBe(trainDataCache.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[2].Data).LabelValue.ShouldBe(trainDataCache.Labels[2]);

         // The data transformers should allow to produce a very shallow tree
         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBe(1);
      }

      [Fact]
      public void DataTransformersQuickExit()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 25, 35 }), 42, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 120, 140 }), 54, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 190, 200 }), 42, Guid.NewGuid());

         PassThroughTransformer passThroughTransformer = new();
         MeanDistanceDataTransformer meanDistanceDataTransformer = new();

         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         dataTransformers += MeanDistanceDataTransformer.ConvertAll;

         for (int i = 0; i < 100; i++)
         {
            dataTransformers += passThroughTransformer.ConvertAll;
         }

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(new TanukiTransformers(dataTransformers, trainDataCache.Samples[0].Data));

         trainDataCache = pakiraDecisionTreeModel.PrefetchAll(trainDataCache);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainDataCache);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0].Data).LabelValue.ShouldBe(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValue.ShouldBe(trainDataCache.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[2].Data).LabelValue.ShouldBe(trainDataCache.Labels[2]);

         // The data transformers should allow to produce a very shallow tree
         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBeInRange(1, 5);
      }

      [Fact]
      public void DataTransformersQuickExit2()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 25, 35 }), 42, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 120, 140 }), 54, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 190, 200 }), 42, Guid.NewGuid());

         MeanDistanceDataTransformer meanDistanceDataTransformer = new();

         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         dataTransformers += MeanDistanceDataTransformer.ConvertAll;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(new TanukiTransformers(dataTransformers, trainDataCache.Samples[0].Data));

         trainDataCache = pakiraDecisionTreeModel.PrefetchAll(trainDataCache);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainDataCache);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0].Data).LabelValue.ShouldBe(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValue.ShouldBe(trainDataCache.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[2].Data).LabelValue.ShouldBe(trainDataCache.Labels[2]);

         // The data transformers should allow to produce a very shallow tree
         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBe(1);
      }

      [Fact]
      public void DeepTree()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 2, 3 }), 42, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 250, 254 }), 54, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 250, 255 }), 42, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 251, 253 }), 6, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 251, 254 }), 9, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 1, 2 }), 96, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 2, 1 }), 97, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 2, 2 }), 98, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 3, 2 }), 99, Guid.NewGuid());

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainDataCache.Samples[0].Data);

         trainDataCache = pakiraDecisionTreeModel.PrefetchAll(trainDataCache);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainDataCache);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         for (int i = 0; i < trainDataCache.Samples.Count; i++)
         {
            pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[i].Data).LabelValue.ShouldBe(trainDataCache.Labels[i], "i=" + i.ToString());
         }
      }

      [Fact]
      public void CertaintyScore()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 2, 90 }), 42, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 54, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 200, 100 }), 42, Guid.NewGuid());

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainDataCache.Samples[0].Data);

         trainDataCache = pakiraDecisionTreeModel.PrefetchAll(trainDataCache);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainDataCache);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0].Data).LabelValue.ShouldBe(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValue.ShouldBe(trainDataCache.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[2].Data).LabelValue.ShouldBe(trainDataCache.Labels[2]);

         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0]).PakiraLeaf.LabelValue.ShouldBe(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1]).PakiraLeaf.LabelValue.ShouldBe(trainDataCache.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[2]).PakiraLeaf.LabelValue.ShouldBe(trainDataCache.Labels[2]);
      }

      [Fact]
      public void GenerateCannotSplit()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 2, 90 }), 42, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 54, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 42, Guid.NewGuid());

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainDataCache.Samples[0].Data);

         trainDataCache = pakiraDecisionTreeModel.PrefetchAll(trainDataCache);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainDataCache);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0].Data).LabelValue.ShouldBe(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValues.Count().ShouldBe(2);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[2].Data).LabelValues.Count().ShouldBe(2);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValues.ShouldContain(trainDataCache.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValues.ShouldContain(trainDataCache.Labels[2]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[2].Data).LabelValues.ShouldContain(trainDataCache.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[2].Data).LabelValues.ShouldContain(trainDataCache.Labels[2]);
      }

      [Fact]
      public void GenerateCannotSplit2()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 54, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 42, Guid.NewGuid());

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainDataCache.Samples[0].Data);

         trainDataCache = pakiraDecisionTreeModel.PrefetchAll(trainDataCache);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainDataCache);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0].Data).LabelValue.ShouldBe(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValues.Count().ShouldBe(2);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0].Data).LabelValues.ShouldContain(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0].Data).LabelValues.ShouldContain(trainDataCache.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValues.ShouldContain(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValues.ShouldContain(trainDataCache.Labels[1]);
      }

      [Fact]
      public void CallPakiraTreeReplaceLeaf()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 2 }), 42, Guid.NewGuid());
         trainDataCache = trainDataCache.AddSample(ImmutableList.CreateRange(new double[] { 2 }), 54, Guid.NewGuid());

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainDataCache.Samples[0].Data);

         trainDataCache = pakiraDecisionTreeModel.PrefetchAll(trainDataCache);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainDataCache);

         TrainDataCache extraDataCache = new();

         extraDataCache = extraDataCache.AddSample(ImmutableList.CreateRange(new double[] { 215 }), 42, Guid.NewGuid());
         extraDataCache = pakiraDecisionTreeModel.PrefetchAll(extraDataCache);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, new TrainDataCache(extraDataCache.Samples[0], extraDataCache.Labels[0], extraDataCache.Guid[0]));

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0].Data).LabelValue.ShouldBe(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValues.Count().ShouldBe(2);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0].Data).LabelValues.ShouldContain(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[0].Data).LabelValues.ShouldContain(trainDataCache.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValues.ShouldContain(trainDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[1].Data).LabelValues.ShouldContain(trainDataCache.Labels[1]);

         pakiraDecisionTreeModel.PredictLeaf(extraDataCache.Samples[0].Data).LabelValue.ShouldBe(extraDataCache.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(extraDataCache.Samples[0].Data).LabelValues.Count().ShouldBe(1);
      }
   }
}
