namespace AmaigomaConsole
{
   using System;
   using System.Collections.Generic;
   using Amaigoma;
   using numl;
   using numl.Math.Probability;
   using numl.Model;
   using numl.Supervised;
   using numl.Supervised.DecisionTree;
   using numl.Tests.Data;

   class Program
   {
      static void Main(string[] args)
      {
         List<Property> features = new List<Property>();

         for (int i = 0; i < 3; i++)
         {
            CustomFeature customFeature = new CustomFeature(i, i + 1);

            customFeature.Name = "Sum";
            customFeature.Type = typeof(System.Double);

            features.Add(customFeature);
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

         List<Iris> samples = new List<Iris>();

         for(int i = 0; i< 1000; i++)
         {
            decimal sepalLength = Convert.ToDecimal(Sampling.GetUniform(0.1, 10.0));
            decimal sepalWidth = Convert.ToDecimal(Sampling.GetUniform(0.1, 6.0));
            decimal petalLength = Convert.ToDecimal(Sampling.GetUniform(0.1, 8.0));
            decimal petalWidth = Convert.ToDecimal(Sampling.GetUniform(0.1, 3.0));

            samples.Add(new Iris { SepalLength = sepalLength, SepalWidth = sepalWidth, PetalLength = petalLength, PetalWidth = petalWidth, Class = string.Empty });
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
         generators.Add(new PakiraGenerator(fluentDescriptor, samples));

         foreach (Generator generator in generators)
         {
            IModel model = generator.Generate(data);

            Iris prediction;

            prediction = model.Predict(data[0]);

            prediction.Class = string.Empty;
            prediction.PetalLength = 9.9m;
            prediction.PetalWidth = 9.9m;
            prediction.SepalLength = 9.9m;
            prediction.SepalWidth = 9.9m;

            prediction = model.Predict(prediction);

            prediction.Class = string.Empty;
            prediction.PetalLength = 0.0m;
            prediction.PetalWidth = 0.0m;
            prediction.SepalLength = 0.0m;
            prediction.SepalWidth = 0.0m;

            prediction = model.Predict(prediction);

            prediction.Class = string.Empty;
            prediction.PetalLength = 5.2m;
            prediction.PetalWidth = 1.9m;
            prediction.SepalLength = 6.0m;
            prediction.SepalWidth = 3.1m;

            prediction = model.Predict(prediction);

            prediction = model.Predict(data[149]);

            LearningModel learned;
            IModel learnedModel;
            double accuracy;
            //  learned = Learner.Learn(data, 0.80, 1, decisionTreeGenerator);
            //  learnedModel = learned.Model;
            //  accuracy = learned.Accuracy;

            for (int i = 0; i < 1; i++)
            {
               learned = Learner.Learn(data, 0.10, 10, generator);
               learnedModel = learned.Model;
               accuracy = learned.Accuracy;

               Console.WriteLine(accuracy.ToString());
            }
         }
      }
   }
}
