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
      [Fact]
      public void Constructor()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
      }

      [Fact]
      public void Generate()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();

         pakiraGenerator.CertaintyScore = 10.0;

         TrainData trainData = new TrainData();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 90 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 200, 100 }), 42);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(trainData.Samples[0]);

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
      public void MinimumSampleCount()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainData trainData = new TrainData();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 3 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 20, 140 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 33, 200 }), 42);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(trainData.Samples[0]);

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
         TrainData trainData = new TrainData();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 3 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 120, 140 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 190, 200 }), 42);

         PassThroughTransformer passThroughTransformer = new PassThroughTransformer();
         MeanDistanceDataTransformer meanDistanceDataTransformer = new MeanDistanceDataTransformer();

         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         dataTransformers += passThroughTransformer.ConvertAll;
         dataTransformers += meanDistanceDataTransformer.ConvertAll;

         pakiraGenerator.CertaintyScore = 10.0;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(dataTransformers, trainData.Samples[0]);

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
         TrainData trainData = new TrainData();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 25, 35 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 120, 140 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 190, 200 }), 42);

         PassThroughTransformer passThroughTransformer = new PassThroughTransformer();
         MeanDistanceDataTransformer meanDistanceDataTransformer = new MeanDistanceDataTransformer();

         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         dataTransformers += meanDistanceDataTransformer.ConvertAll;

         for (int i = 0; i < 100; i++)
         {
            dataTransformers += passThroughTransformer.ConvertAll;
         }

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(dataTransformers, trainData.Samples[0]);

         pakiraGenerator.MinimumSampleCount = 250;

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
         TrainData trainData = new TrainData();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 25, 35 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 120, 140 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 190, 200 }), 42);

         MeanDistanceDataTransformer meanDistanceDataTransformer = new MeanDistanceDataTransformer();

         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         dataTransformers += meanDistanceDataTransformer.ConvertAll;

         pakiraGenerator.CertaintyScore = 10.0;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(dataTransformers, trainData.Samples[0]);

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
         TrainData trainData = new TrainData();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 3 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 250, 254 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 250, 255 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 251, 253 }), 6);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 251, 254 }), 9);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 1, 2 }), 96);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 1 }), 97);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 2 }), 98);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 3, 2 }), 99);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(trainData.Samples[0]);

         pakiraGenerator.MinimumSampleCount = 100;

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBeGreaterThanOrEqualTo(3, "If the test fails because of this, the number can be reduced as long as it stays 'high'. Instead, the tree depth could also be validated.");
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[0]).LabelValue.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[1]).LabelValue.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[2]).LabelValue.ShouldBe(trainData.Labels[2]);
      }

      [Fact]
      public void CertaintyScore()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();

         pakiraGenerator.CertaintyScore = 1.0;

         TrainData trainData = new TrainData();

         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 2, 90 }), 42);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 250, 140 }), 54);
         trainData = trainData.AddSample(ImmutableList.CreateRange(new double[] { 200, 100 }), 42);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[0]).LabelValue.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[1]).LabelValue.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(trainData.Samples[2]).LabelValue.ShouldBe(trainData.Labels[2]);

         pakiraDecisionTreeModel.PredictLeaf(new SabotenCache(trainData.Samples[0])).PakiraLeaf.LabelValue.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictLeaf(new SabotenCache(trainData.Samples[1])).PakiraLeaf.LabelValue.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictLeaf(new SabotenCache(trainData.Samples[2])).PakiraLeaf.LabelValue.ShouldBe(trainData.Labels[2]);
      }

      internal class MeanDistanceDataTransformer
      {
         public MeanDistanceDataTransformer()
         {
         }

         public IEnumerable<double> ConvertAll(IEnumerable<double> list)
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

      static public PakiraDecisionTreeGenerator CreatePakiraGeneratorInstance()
      {
         PakiraDecisionTreeGenerator pakiraDecisionTreeGenerator = new PakiraDecisionTreeGenerator();

         Console.WriteLine("PakiraDecisionTreeGenerator random seed: " + PakiraDecisionTreeGenerator.randomSeed.ToString());

         return pakiraDecisionTreeGenerator;
      }
   }
}
