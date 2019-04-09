namespace AmaigomaConsole
{
   using Amaigoma;
   using numl;
   using numl.Math.Probability;
   using numl.Model;
   using numl.Supervised;
   using numl.Tests.Data;
   using System;
   using System.Collections.Generic;

   class Program
   {
      static void Main(string[] args)
      {
         List<Property> features = new List<Property>();

         for (int i = 0; i < 3; i++)
         {
            SumFeature sumFeature = new SumFeature(i, i + 1)
            {
               Name = "Sum" + i.ToString(),
               Type = typeof(System.Double)
            };

            features.Add(sumFeature);

            ProductFeature productFeature = new ProductFeature(i, i + 1)
            {
               Name = "Product" + i.ToString(),
               Type = typeof(System.Double)
            };

            features.Add(productFeature);
         }

         for (int i = 0; i < 100; i++)
         {
            RandomFeature randomFeature = new RandomFeature()
            {
               Name = "Random" + i.ToString(),
               Type = typeof(System.Double)
            };

            //features.Add(randomFeature);
         }

         Descriptor description = Descriptor.Create<Iris>();
         Descriptor fluentDescriptor = Descriptor.New(typeof(Iris))
                                                   .With("SepalLength").As(typeof(decimal))
                                                   .With("SepalWidth").As(typeof(double))
                                                   .With("PetalLength").As(typeof(decimal))
                                                   .With("PetalWidth").As(typeof(int))
                                                   //.With("Sum").As(customFeature)
                                                   .Learn("Class").As(typeof(string));

         features.AddRange(fluentDescriptor.Features);

         fluentDescriptor.Features = features.ToArray();

         Console.WriteLine(description);
         Console.WriteLine(fluentDescriptor);

         Iris[] data = Iris.Load();
         List<Generator> generators = new List<Generator>();

         List<Iris> samples1000 = new List<Iris>();
         List<Iris> samples5000 = new List<Iris>();
         List<Iris> samples10000 = new List<Iris>();

         for (int i = 0; i < 1000; i++)
         {
            decimal sepalLength = Convert.ToDecimal(Sampling.GetUniform(0.1, 10.0));
            decimal sepalWidth = Convert.ToDecimal(Sampling.GetUniform(0.1, 6.0));
            decimal petalLength = Convert.ToDecimal(Sampling.GetUniform(0.1, 8.0));
            decimal petalWidth = Convert.ToDecimal(Sampling.GetUniform(0.1, 3.0));

            samples1000.Add(new Iris { SepalLength = sepalLength, SepalWidth = sepalWidth, PetalLength = petalLength, PetalWidth = petalWidth, Class = string.Empty });
         }

         for (int i = 0; i < 5000; i++)
         {
            decimal sepalLength = Convert.ToDecimal(Sampling.GetUniform(0.1, 10.0));
            decimal sepalWidth = Convert.ToDecimal(Sampling.GetUniform(0.1, 6.0));
            decimal petalLength = Convert.ToDecimal(Sampling.GetUniform(0.1, 8.0));
            decimal petalWidth = Convert.ToDecimal(Sampling.GetUniform(0.1, 3.0));

            samples5000.Add(new Iris { SepalLength = sepalLength, SepalWidth = sepalWidth, PetalLength = petalLength, PetalWidth = petalWidth, Class = string.Empty });
         }

         for (int i = 0; i < 10000; i++)
         {
            decimal sepalLength = Convert.ToDecimal(Sampling.GetUniform(0.1, 10.0));
            decimal sepalWidth = Convert.ToDecimal(Sampling.GetUniform(0.1, 6.0));
            decimal petalLength = Convert.ToDecimal(Sampling.GetUniform(0.1, 8.0));
            decimal petalWidth = Convert.ToDecimal(Sampling.GetUniform(0.1, 3.0));

            samples10000.Add(new Iris { SepalLength = sepalLength, SepalWidth = sepalWidth, PetalLength = petalLength, PetalWidth = petalWidth, Class = string.Empty });
         }


         // generators.Add(new PakiraGenerator());
         //generators.Add(new DecisionTreeGenerator(description));
         //generators.Add(new DecisionTreeGenerator(10, 2, description));
         //generators.Add(new DecisionTreeGenerator(fluentDescriptor));
         //generators.Add(new NaiveBayesGenerator(2));
         //generators.Add(new NaiveBayesGenerator(3));
         //generators.Add(new NaiveBayesGenerator(5));
         //generators.Add(new NaiveBayesGenerator(8));
         //generators.Add(new NaiveBayesGenerator(13));
         //generators.Add(new NaiveBayesGenerator(21));
         //generators.Add(new PakiraGenerator(fluentDescriptor, samples1000, PakiraGenerator.UNKNOWN_CLASS_INDEX, 10));
         //generators.Add(new PakiraGenerator(fluentDescriptor, samples5000, PakiraGenerator.UNKNOWN_CLASS_INDEX, 10));
         generators.Add(new PakiraGenerator(fluentDescriptor, samples10000, PakiraGenerator.UNKNOWN_CLASS_INDEX, 10));

         PakiraGenerator pakiraGenerator = new PakiraGenerator(fluentDescriptor, samples10000, PakiraGenerator.UNKNOWN_CLASS_INDEX, 10);
         PakiraModel pakiraModel;

         pakiraModel = pakiraGenerator.Generate(new List<Iris>() { data[0]});

         Console.WriteLine("Model " + pakiraModel.ToString());


         int generatorIndex = 0;

         foreach (Generator generator in generators)
         {
            //IModel model;

            //if (generator.Descriptor == null)
            //{
            //   model = generator.Generate(description, data);
            //}
            //else
            //{
            //   model = generator.Generate(data);
            //}

            //Console.WriteLine("Model " + model.ToString());

            //Iris prediction;

            //prediction = model.Predict(data[0]);

            //prediction.Class = string.Empty;
            //prediction.PetalLength = 9.9m;
            //prediction.PetalWidth = 9.9m;
            //prediction.SepalLength = 9.9m;
            //prediction.SepalWidth = 9.9m;

            //prediction = model.Predict(prediction);

            //prediction.Class = string.Empty;
            //prediction.PetalLength = 0.0m;
            //prediction.PetalWidth = 0.0m;
            //prediction.SepalLength = 0.0m;
            //prediction.SepalWidth = 0.0m;

            //prediction = model.Predict(prediction);

            //prediction.Class = string.Empty;
            //prediction.PetalLength = 5.2m;
            //prediction.PetalWidth = 1.9m;
            //prediction.SepalLength = 6.0m;
            //prediction.SepalWidth = 3.1m;

            //prediction = model.Predict(prediction);

            //prediction = model.Predict(data[149]);

            LearningModel learned;
            IModel learnedModel;
            double accuracy;
            //  learned = Learner.Learn(data, 0.80, 1, decisionTreeGenerator);
            //  learnedModel = learned.Model;
            //  accuracy = learned.Accuracy;

            Console.WriteLine("Analyzing generator index " + generatorIndex++);
            double minAccuracy = double.MaxValue;
            double maxAccuracy = double.MinValue;
            double sumAccuracy = 0.0;
            const int learnCount = 1;

            for (int i = 0; i < learnCount; i++)
            {
               learned = Learner.Learn(data, 0.10, 1, generator);
               learnedModel = learned.Model;
               accuracy = learned.Accuracy;

               minAccuracy = Math.Min(minAccuracy, accuracy);
               maxAccuracy = Math.Max(maxAccuracy, accuracy);
               sumAccuracy += accuracy;

               Console.WriteLine(accuracy.ToString());
               Console.WriteLine("Model " + learnedModel.ToString());
            }

            Console.WriteLine("Min: " + minAccuracy.ToString());
            Console.WriteLine("Max: " + maxAccuracy.ToString());
            Console.WriteLine("Average: " + (sumAccuracy / learnCount).ToString());

            Console.WriteLine();
         }
      }
   }
}
