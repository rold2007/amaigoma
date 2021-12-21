namespace AmaigomaTests
{
   using Amaigoma;
   using MathNet.Numerics.LinearAlgebra;
   using Shouldly;
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using Xunit;

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

         pakiraGenerator.CertaintyScore = 1.0;

         TrainData trainData = new TrainData();

         trainData = trainData.AddSample(new List<double> { 2, 90 }, 42);
         trainData = trainData.AddSample(new List<double> { 250, 140 }, 54);
         trainData = trainData.AddSample(new List<double> { 200, 100 }, 42);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictNode(trainData.Samples[0]).Value.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictNode(trainData.Samples[1]).Value.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictNode(trainData.Samples[2]).Value.ShouldBe(trainData.Labels[2]);

         pakiraDecisionTreeModel.PredictNode(new SabotenCache(trainData.Samples[0])).PakiraLeaf.Value.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictNode(new SabotenCache(trainData.Samples[1])).PakiraLeaf.Value.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictNode(new SabotenCache(trainData.Samples[2])).PakiraLeaf.Value.ShouldBe(trainData.Labels[2]);
      }

      [Fact]
      public void MinimumSampleCount()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainData trainData = new TrainData();

         trainData = trainData.AddSample(new List<double> { 2, 3 }, 42);
         trainData = trainData.AddSample(new List<double> { 20, 140 }, 54);
         trainData = trainData.AddSample(new List<double> { 33, 200 }, 42);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictNode(trainData.Samples[0]).Value.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictNode(trainData.Samples[1]).Value.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictNode(trainData.Samples[2]).Value.ShouldBe(trainData.Labels[2]);
      }

      [Fact]
      public void DataTransformers()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainData trainData = new TrainData();

         trainData = trainData.AddSample(new List<double> { 2, 3 }, 42);
         trainData = trainData.AddSample(new List<double> { 120, 140 }, 54);
         trainData = trainData.AddSample(new List<double> { 190, 200 }, 42);

         PassThroughTransformer passThroughTransformer = new PassThroughTransformer();
         MeanDistanceDataTransformer meanDistanceDataTransformer = new MeanDistanceDataTransformer();

         Converter<IList<double>, IList<double>> dataTransformers = null;

         dataTransformers += passThroughTransformer.ConvertAll;
         dataTransformers += meanDistanceDataTransformer.ConvertAll;

         pakiraGenerator.CertaintyScore = 1.0;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(dataTransformers, trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictNode(trainData.Samples[0]).Value.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictNode(trainData.Samples[1]).Value.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictNode(trainData.Samples[2]).Value.ShouldBe(trainData.Labels[2]);

         // The data transformers should allow to produce a very shallow tree
         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBe(3);
      }

      [Fact]
      public void DataTransformersQuickExit()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainData trainData = new TrainData();

         trainData = trainData.AddSample(new List<double> { 25, 35 }, 42);
         trainData = trainData.AddSample(new List<double> { 120, 140 }, 54);
         trainData = trainData.AddSample(new List<double> { 190, 200 }, 42);

         PassThroughTransformer passThroughTransformer = new PassThroughTransformer();
         MeanDistanceDataTransformer meanDistanceDataTransformer = new MeanDistanceDataTransformer();

         Converter<IList<double>, IList<double>> dataTransformers = null;

         dataTransformers += meanDistanceDataTransformer.ConvertAll;

         for (int i = 0; i < 100; i++)
         {
            dataTransformers += passThroughTransformer.ConvertAll;
         }

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(dataTransformers, trainData.Samples[0]);

         pakiraGenerator.MinimumSampleCount = 250;

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictNode(trainData.Samples[0]).Value.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictNode(trainData.Samples[1]).Value.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictNode(trainData.Samples[2]).Value.ShouldBe(trainData.Labels[2]);

         // The data transformers should allow to produce a very shallow tree
         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBeInRange(3, 7);
      }

      [Fact]
      public void DataTransformersQuickExit2()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainData trainData = new TrainData();

         trainData = trainData.AddSample(new List<double> { 25, 35 }, 42);
         trainData = trainData.AddSample(new List<double> { 120, 140 }, 54);
         trainData = trainData.AddSample(new List<double> { 190, 200 }, 42);

         MeanDistanceDataTransformer meanDistanceDataTransformer = new MeanDistanceDataTransformer();

         Converter<IList<double>, IList<double>> dataTransformers = null;

         dataTransformers += meanDistanceDataTransformer.ConvertAll;

         pakiraGenerator.CertaintyScore = 1.0;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(dataTransformers, trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictNode(trainData.Samples[0]).Value.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictNode(trainData.Samples[1]).Value.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictNode(trainData.Samples[2]).Value.ShouldBe(trainData.Labels[2]);

         // The data transformers should allow to produce a very shallow tree
         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBe(3);
      }

      [Fact]
      public void DeepTree()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         TrainData trainData = new TrainData();

         trainData = trainData.AddSample(new List<double> { 2, 3 }, 42);
         trainData = trainData.AddSample(new List<double> { 250, 254 }, 54);
         trainData = trainData.AddSample(new List<double> { 250, 255 }, 42);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(trainData.Samples[0]);

         pakiraGenerator.MinimumSampleCount = 100;

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBeGreaterThanOrEqualTo(15, "If the test fails because of this, the number can be reduced as long as it stays 'high'. Instead, the tree depth could also be validated.");
         pakiraDecisionTreeModel.PredictNode(trainData.Samples[0]).Value.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictNode(trainData.Samples[1]).Value.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictNode(trainData.Samples[2]).Value.ShouldBe(trainData.Labels[2]);
      }

      [Fact]
      public void CertaintyScore()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();

         pakiraGenerator.CertaintyScore = 0.5;

         TrainData trainData = new TrainData();

         trainData = trainData.AddSample(new List<double> { 2, 90 }, 42);
         trainData = trainData.AddSample(new List<double> { 250, 140 }, 54);
         trainData = trainData.AddSample(new List<double> { 200, 100 }, 42);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.PredictNode(trainData.Samples[0]).Value.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictNode(trainData.Samples[1]).Value.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictNode(trainData.Samples[2]).Value.ShouldBe(trainData.Labels[2]);

         pakiraDecisionTreeModel.PredictNode(new SabotenCache(trainData.Samples[0])).PakiraLeaf.Value.ShouldBe(trainData.Labels[0]);
         pakiraDecisionTreeModel.PredictNode(new SabotenCache(trainData.Samples[1])).PakiraLeaf.Value.ShouldBe(trainData.Labels[1]);
         pakiraDecisionTreeModel.PredictNode(new SabotenCache(trainData.Samples[2])).PakiraLeaf.Value.ShouldBe(trainData.Labels[2]);
      }

      internal class MeanDistanceDataTransformer
      {
         public MeanDistanceDataTransformer()
         {
         }

         public IList<double> ConvertAll(IList<double> list)
         {
            List<double> result = new List<double>(list.Count() - 1);

            for (int i = 0; i < list.Count() - 1; i++)
            {
               double add = list[i] + list[i + 1];

               add = 255 - add;
               add = Math.Abs(add);

               result.Add(add);
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
