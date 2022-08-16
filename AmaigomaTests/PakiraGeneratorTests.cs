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

         TrainData trainData = new();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 90 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 200, 100 }), 42);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[0]).LabelValue.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[1]).LabelValue.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[2]).LabelValue.ShouldBe(trainData.Labels[2]);

         pakiraDecisionTreeModel.PredictLeaf(new SabotenCache(trainData.Samples[0])).PakiraLeaf.LabelValue.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(new SabotenCache(trainData.Samples[1])).PakiraLeaf.LabelValue.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(new SabotenCache(trainData.Samples[2])).PakiraLeaf.LabelValue.ShouldBe(trainData.Labels[2]);
      }

      [Fact]
      public void GenerateMultipleCalls()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();

         TrainData trainData = new();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 90 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 200, 100 }), 42);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         TrainData trainData2 = new();

         trainData2 = trainData2.AddSample(ImmutableList.CreateRange(new double[] { 3, 91 }), 42);
         trainData2 = trainData2.AddSample(ImmutableList.CreateRange(new double[] { 128, 95 }), 54);
         trainData2 = trainData2.AddSample(ImmutableList.CreateRange(new double[] { 201, 101 }), 42);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData2);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[0]).LabelValue.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[1]).LabelValue.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[2]).LabelValue.ShouldBe(trainData.Labels[2]);

         pakiraDecisionTreeModel.PredictLeaf(new SabotenCache(trainData.Samples[0])).PakiraLeaf.LabelValue.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(new SabotenCache(trainData.Samples[1])).PakiraLeaf.LabelValue.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(new SabotenCache(trainData.Samples[2])).PakiraLeaf.LabelValue.ShouldBe(trainData.Labels[2]);

         pakiraDecisionTreeModel.PredictLeaf(trainData2.Samples[0]).LabelValue.ShouldBe(trainData2.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainData2.Samples[1]).LabelValue.ShouldBe(trainData2.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainData2.Samples[2]).LabelValue.ShouldBe(trainData2.Labels[2]);

         pakiraDecisionTreeModel.PredictLeaf(new SabotenCache(trainData2.Samples[0])).PakiraLeaf.LabelValue.ShouldBe(trainData2.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(new SabotenCache(trainData2.Samples[1])).PakiraLeaf.LabelValue.ShouldBe(trainData2.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(new SabotenCache(trainData2.Samples[2])).PakiraLeaf.LabelValue.ShouldBe(trainData2.Labels[2]);
      }

      [Fact]
      public void MinimumSampleCount()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainData trainData = new();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 3 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 20, 140 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 33, 200 }), 42);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[0]).LabelValue.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[1]).LabelValue.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[2]).LabelValue.ShouldBe(trainData.Labels[2]);
      }

      [Fact]
      public void DataTransformers()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainData trainData = new();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 3 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 120, 140 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 190, 200 }), 42);

         PassThroughTransformer passThroughTransformer = new();
         MeanDistanceDataTransformer meanDistanceDataTransformer = new();

         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         dataTransformers += passThroughTransformer.ConvertAll;
         dataTransformers += MeanDistanceDataTransformer.ConvertAll;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(dataTransformers, trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[0]).LabelValue.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[1]).LabelValue.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[2]).LabelValue.ShouldBe(trainData.Labels[2]);

         // The data transformers should allow to produce a very shallow tree
         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBe(1);
      }

      [Fact]
      public void DataTransformersQuickExit()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainData trainData = new();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 25, 35 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 120, 140 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 190, 200 }), 42);

         PassThroughTransformer passThroughTransformer = new();
         MeanDistanceDataTransformer meanDistanceDataTransformer = new();

         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         dataTransformers += MeanDistanceDataTransformer.ConvertAll;

         for (int i = 0; i < 100; i++)
         {
            dataTransformers += passThroughTransformer.ConvertAll;
         }

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(dataTransformers, trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[0]).LabelValue.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[1]).LabelValue.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[2]).LabelValue.ShouldBe(trainData.Labels[2]);

         // The data transformers should allow to produce a very shallow tree
         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBeInRange(1, 5);
      }

      [Fact]
      public void DataTransformersQuickExit2()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainData trainData = new();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 25, 35 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 120, 140 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 190, 200 }), 42);

         MeanDistanceDataTransformer meanDistanceDataTransformer = new();

         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         dataTransformers += MeanDistanceDataTransformer.ConvertAll;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(dataTransformers, trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[0]).LabelValue.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[1]).LabelValue.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[2]).LabelValue.ShouldBe(trainData.Labels[2]);

         // The data transformers should allow to produce a very shallow tree
         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBe(1);
      }

      [Fact]
      public void DeepTree()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainData trainData = new();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 3 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 250, 254 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 250, 255 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 251, 253 }), 6);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 251, 254 }), 9);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 1, 2 }), 96);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 1 }), 97);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 2 }), 98);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 3, 2 }), 99);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         for (int i = 0; i < trainData.Samples.Count; i++)
         {
            pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[i]).LabelValue.ShouldBe(trainData.Labels[i], "i=" + i.ToString());
         }
      }

      [Fact]
      public void CertaintyScore()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();

         TrainData trainData = new();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 90 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 200, 100 }), 42);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[0]).LabelValue.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[1]).LabelValue.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[2]).LabelValue.ShouldBe(trainData.Labels[2]);

         pakiraDecisionTreeModel.PredictLeaf(new SabotenCache(trainData.Samples[0])).PakiraLeaf.LabelValue.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(new SabotenCache(trainData.Samples[1])).PakiraLeaf.LabelValue.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(new SabotenCache(trainData.Samples[2])).PakiraLeaf.LabelValue.ShouldBe(trainData.Labels[2]);
      }

      [Fact]
      public void GenerateCannotSplit()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();

         TrainData trainData = new();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 90 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 42);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[0]).LabelValue.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[1]).LabelValues.Count().ShouldBe(2);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[2]).LabelValues.Count().ShouldBe(2);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[1]).LabelValues.ShouldContain(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[1]).LabelValues.ShouldContain(trainData.Labels[2]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[2]).LabelValues.ShouldContain(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[2]).LabelValues.ShouldContain(trainData.Labels[2]);
      }

      [Fact]
      public void GenerateCannotSplit2()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();

         TrainData trainData = new();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 42);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[0]).LabelValue.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[1]).LabelValues.Count().ShouldBe(2);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[0]).LabelValues.ShouldContain(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[0]).LabelValues.ShouldContain(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[1]).LabelValues.ShouldContain(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[1]).LabelValues.ShouldContain(trainData.Labels[1]);
      }
   }
}
