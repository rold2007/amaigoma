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
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel();
         const int featureCount = 2;
         const int sampleCount = 3;
         Matrix<double> samples = Matrix<double>.Build.Dense(sampleCount, featureCount);
         Vector<double> labels = Vector<double>.Build.Dense(sampleCount);

         // Sample 0
         samples.At(0, 0, 2.0);
         samples.At(0, 1, 90.0);

         // Sample 1
         samples.At(1, 0, 250.0);
         samples.At(1, 1, 140.0);

         // Sample 2
         samples.At(2, 0, 200.0);
         samples.At(2, 1, 100.0);

         labels.At(0, 42);
         labels.At(1, 54);
         labels.At(2, 42);

         pakiraGenerator.CertaintyScore = 1.0;

         pakiraGenerator.Generate(pakiraDecisionTreeModel, samples.EnumerateRows(), labels);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.Predict(samples.Row(0)).ShouldBe(labels.At(0));
         pakiraDecisionTreeModel.Predict(samples.Row(1)).ShouldBe(labels.At(1));
         pakiraDecisionTreeModel.Predict(samples.Row(2)).ShouldBe(labels.At(2));

         pakiraDecisionTreeModel.Predict(new SabotenCache(samples.Row(0))).ShouldBe(labels.At(0));
         pakiraDecisionTreeModel.Predict(new SabotenCache(samples.Row(1))).ShouldBe(labels.At(1));
         pakiraDecisionTreeModel.Predict(new SabotenCache(samples.Row(2))).ShouldBe(labels.At(2));
      }

      [Fact]
      public void MinimumSampleCount()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel();
         const int featureCount = 2;
         const int sampleCount = 3;
         Matrix<double> samples = Matrix<double>.Build.Dense(sampleCount, featureCount);
         Vector<double> labels = Vector<double>.Build.Dense(sampleCount);

         // Sample 0
         samples.At(0, 0, 2.0);
         samples.At(0, 1, 3.0);

         // Sample 1
         samples.At(1, 0, 20.0);
         samples.At(1, 1, 140.0);

         // Sample 2
         samples.At(2, 0, 33.0);
         samples.At(2, 1, 200.0);

         labels.At(0, 42);
         labels.At(1, 54);
         labels.At(2, 42);

         pakiraGenerator.Generate(pakiraDecisionTreeModel, samples.EnumerateRows(), labels);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.Predict(samples.Row(0)).ShouldBe(labels.At(0));
         pakiraDecisionTreeModel.Predict(samples.Row(1)).ShouldBe(labels.At(1));
         pakiraDecisionTreeModel.Predict(samples.Row(2)).ShouldBe(labels.At(2));
      }

      [Fact]
      public void DataTransformers()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel();
         const int featureCount = 2;
         const int sampleCount = 3;
         Matrix<double> samples = Matrix<double>.Build.Dense(sampleCount, featureCount);
         Vector<double> labels = Vector<double>.Build.Dense(sampleCount);

         // Sample 0
         samples.At(0, 0, 2.0);
         samples.At(0, 1, 3.0);

         // Sample 1
         samples.At(1, 0, 120.0);
         samples.At(1, 1, 140.0);

         // Sample 2
         samples.At(2, 0, 190.0);
         samples.At(2, 1, 200.0);

         labels.At(0, 42);
         labels.At(1, 54);
         labels.At(2, 42);

         PassThroughTransformer passThroughTransformer = new PassThroughTransformer();
         MeanDistanceDataTransformer meanDistanceDataTransformer = new MeanDistanceDataTransformer();

         Converter<IList<double>, IList<double>> dataTransformers = null;

         dataTransformers += passThroughTransformer.ConvertAll;
         dataTransformers += meanDistanceDataTransformer.ConvertAll;

         pakiraGenerator.CertaintyScore = 1.0;

         pakiraGenerator.Generate(pakiraDecisionTreeModel, samples.EnumerateRows(), labels, dataTransformers);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.Predict(samples.Row(0)).ShouldBe(labels.At(0));
         pakiraDecisionTreeModel.Predict(samples.Row(1)).ShouldBe(labels.At(1));
         pakiraDecisionTreeModel.Predict(samples.Row(2)).ShouldBe(labels.At(2));

         // The data transformers should allow to produce a very shallow tree
         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBe(3);
      }

      [Fact]
      public void DataTransformersQuickExit()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel();
         const int featureCount = 2;
         const int sampleCount = 3;
         Matrix<double> samples = Matrix<double>.Build.Dense(sampleCount, featureCount);
         Vector<double> labels = Vector<double>.Build.Dense(sampleCount);

         // Sample 0
         samples.At(0, 0, 25.0);
         samples.At(0, 1, 35.0);

         // Sample 1
         samples.At(1, 0, 120.0);
         samples.At(1, 1, 140.0);

         // Sample 2
         samples.At(2, 0, 190.0);
         samples.At(2, 1, 200.0);

         labels.At(0, 42);
         labels.At(1, 54);
         labels.At(2, 42);

         PassThroughTransformer passThroughTransformer = new PassThroughTransformer();
         MeanDistanceDataTransformer meanDistanceDataTransformer = new MeanDistanceDataTransformer();

         Converter<IList<double>, IList<double>> dataTransformers = null;

         dataTransformers += meanDistanceDataTransformer.ConvertAll;

         for (int i = 0; i < 100; i++)
         {
            dataTransformers += passThroughTransformer.ConvertAll;
         }

         pakiraGenerator.Generate(pakiraDecisionTreeModel, samples.EnumerateRows(), labels, dataTransformers);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.Predict(samples.Row(0)).ShouldBe(labels.At(0));
         pakiraDecisionTreeModel.Predict(samples.Row(1)).ShouldBe(labels.At(1));
         pakiraDecisionTreeModel.Predict(samples.Row(2)).ShouldBe(labels.At(2));

         // The data transformers should allow to produce a very shallow tree
         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBe(3);
      }

      [Fact]
      public void DataTransformersQuickExit2()
      {
         PakiraDecisionTreeGenerator pakiraGenerator = PakiraGeneratorTests.CreatePakiraGeneratorInstance();
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel();
         const int featureCount = 2;
         const int sampleCount = 3;
         Matrix<double> samples = Matrix<double>.Build.Dense(sampleCount, featureCount);
         Vector<double> labels = Vector<double>.Build.Dense(sampleCount);

         // Sample 0
         samples.At(0, 0, 25.0);
         samples.At(0, 1, 35.0);

         // Sample 1
         samples.At(1, 0, 120.0);
         samples.At(1, 1, 140.0);

         // Sample 2
         samples.At(2, 0, 190.0);
         samples.At(2, 1, 200.0);

         labels.At(0, 42);
         labels.At(1, 54);
         labels.At(2, 42);

         MeanDistanceDataTransformer meanDistanceDataTransformer = new MeanDistanceDataTransformer();

         Converter<IList<double>, IList<double>> dataTransformers = null;

         dataTransformers += meanDistanceDataTransformer.ConvertAll;

         pakiraGenerator.CertaintyScore = 1.0;

         pakiraGenerator.Generate(pakiraDecisionTreeModel, samples.EnumerateRows(), labels, dataTransformers);

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         pakiraDecisionTreeModel.Predict(samples.Row(0)).ShouldBe(labels.At(0));
         pakiraDecisionTreeModel.Predict(samples.Row(1)).ShouldBe(labels.At(1));
         pakiraDecisionTreeModel.Predict(samples.Row(2)).ShouldBe(labels.At(2));

         // The data transformers should allow to produce a very shallow tree
         pakiraDecisionTreeModel.Tree.GetNodes().Count().ShouldBe(3);
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

               add = 256 - add;
               add = Math.Abs(add);

               result.Add(add);
            }

            return result;
         }
      }

      static public PakiraDecisionTreeGenerator CreatePakiraGeneratorInstance()
      {
         return new PakiraDecisionTreeGenerator();
      }
   }
}
