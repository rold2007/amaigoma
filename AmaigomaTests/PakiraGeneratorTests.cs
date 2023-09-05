using Amaigoma;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace AmaigomaTests
{
   public class PakiraGeneratorTests
   {
      internal class MeanDistanceDataTransformer
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
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();

         TrainDataCache trainDataCache = new TrainDataCache();

         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 2, 90 }))), ImmutableList<double>.Empty.Add(42)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 250, 140 }))), ImmutableList<double>.Empty.Add(54)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 200, 100 }))), ImmutableList<double>.Empty.Add(42)));

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
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();

         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 2, 90 }))), ImmutableList<double>.Empty.Add(42)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 250, 140 }))), ImmutableList<double>.Empty.Add(54)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 200, 100 }))), ImmutableList<double>.Empty.Add(42)));

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainDataCache.Samples[0].Data);

         trainDataCache = pakiraDecisionTreeModel.PrefetchAll(trainDataCache);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainDataCache);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         TrainDataCache trainDataCache2 = new();

         trainDataCache2 = trainDataCache2.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 3, 91 }))), ImmutableList<double>.Empty.Add(42)));
         trainDataCache2 = trainDataCache2.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 128, 95 }))), ImmutableList<double>.Empty.Add(54)));
         trainDataCache2 = trainDataCache2.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 201, 101 }))), ImmutableList<double>.Empty.Add(42)));

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
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 2, 3 }))), ImmutableList<double>.Empty.Add(42)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 20, 140 }))), ImmutableList<double>.Empty.Add(54)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 33, 200 }))), ImmutableList<double>.Empty.Add(42)));

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
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 2, 3 }))), ImmutableList<double>.Empty.Add(42)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 120, 140 }))), ImmutableList<double>.Empty.Add(54)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 190, 200 }))), ImmutableList<double>.Empty.Add(42)));

         PassThroughTransformer passThroughTransformer = new();
         MeanDistanceDataTransformer meanDistanceDataTransformer = new();

         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         dataTransformers += passThroughTransformer.ConvertAll;
         dataTransformers += MeanDistanceDataTransformer.ConvertAll;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(dataTransformers, trainDataCache.Samples[0].Data);

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
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 25, 35 }))), ImmutableList<double>.Empty.Add(42)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 120, 140 }))), ImmutableList<double>.Empty.Add(54)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 190, 200 }))), ImmutableList<double>.Empty.Add(42)));

         PassThroughTransformer passThroughTransformer = new();
         MeanDistanceDataTransformer meanDistanceDataTransformer = new();

         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         dataTransformers += MeanDistanceDataTransformer.ConvertAll;

         for (int i = 0; i < 100; i++)
         {
            dataTransformers += passThroughTransformer.ConvertAll;
         }

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(dataTransformers, trainDataCache.Samples[0].Data);

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
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 25, 35 }))), ImmutableList<double>.Empty.Add(42)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 120, 140 }))), ImmutableList<double>.Empty.Add(54)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 190, 200 }))), ImmutableList<double>.Empty.Add(42)));

         MeanDistanceDataTransformer meanDistanceDataTransformer = new();

         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         dataTransformers += MeanDistanceDataTransformer.ConvertAll;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(dataTransformers, trainDataCache.Samples[0].Data);

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
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 2, 3 }))), ImmutableList<double>.Empty.Add(42)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 250, 254 }))), ImmutableList<double>.Empty.Add(54)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 250, 255 }))), ImmutableList<double>.Empty.Add(42)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 251, 253 }))), ImmutableList<double>.Empty.Add(6)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 251, 254 }))), ImmutableList<double>.Empty.Add(9)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 1, 2 }))), ImmutableList<double>.Empty.Add(96)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 2, 1 }))), ImmutableList<double>.Empty.Add(97)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 2, 2 }))), ImmutableList<double>.Empty.Add(98)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 3, 2 }))), ImmutableList<double>.Empty.Add(99)));

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
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 2, 90 }))), ImmutableList<double>.Empty.Add(42)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 250, 140 }))), ImmutableList<double>.Empty.Add(54)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 200, 100 }))), ImmutableList<double>.Empty.Add(42)));

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
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 2, 90 }))), ImmutableList<double>.Empty.Add(42)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 250, 140 }))), ImmutableList<double>.Empty.Add(54)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 250, 140 }))), ImmutableList<double>.Empty.Add(42)));

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
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainDataCache trainDataCache = new();

         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 250, 140 }))), ImmutableList<double>.Empty.Add(54)));
         trainDataCache = trainDataCache.AddSamples(new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(new SabotenCache(ImmutableList.CreateRange(new double[] { 250, 140 }))), ImmutableList<double>.Empty.Add(42)));

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
   }
}
