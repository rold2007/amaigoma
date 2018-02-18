namespace AmaigomaConsole
{
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using System.Text;
   using System.Threading.Tasks;
   using Amaigoma;
   using numl;
   using numl.Model;
   using numl.Supervised;
   using numl.Supervised.DecisionTree;
   using numl.Supervised.NaiveBayes;
   using numl.Tests.Data;

   class Program
   {
      static void Main(string[] args)
      {
         Descriptor description = Descriptor.Create<Iris>();

         Console.WriteLine(description);

         Iris[] data = Iris.Load();
         List<Generator> generators = new List<Generator>
         {
            //new PakiraGenerator(),
            //new DecisionTreeGenerator(description),
            new NaiveBayesGenerator(2),
            new NaiveBayesGenerator(3),
            new NaiveBayesGenerator(5),
            new NaiveBayesGenerator(8),
            new NaiveBayesGenerator(13),
            new NaiveBayesGenerator(21)
         };

         foreach (Generator generator in generators)
         {
            IModel model = generator.Generate(description, data);

            Iris prediction;

            prediction = data[0];

            //prediction = decisionTreeModel.Predict(prediction);
            prediction = model.Predict(prediction);

            prediction.Class = string.Empty;
            prediction.PetalLength = 9.9m;
            prediction.PetalWidth = 9.9m;
            prediction.SepalLength = 9.9m;
            prediction.SepalWidth = 9.9m;

            //prediction = decisionTreeModel.Predict(prediction);
            prediction = model.Predict(prediction);

            prediction.Class = string.Empty;
            prediction.PetalLength = 0.0m;
            prediction.PetalWidth = 0.0m;
            prediction.SepalLength = 0.0m;
            prediction.SepalWidth = 0.0m;

            //prediction = decisionTreeModel.Predict(prediction);
            prediction = model.Predict(prediction);

            prediction.Class = string.Empty;
            prediction.PetalLength = 5.2m;
            prediction.PetalWidth = 1.9m;
            prediction.SepalLength = 6.0m;
            prediction.SepalWidth = 3.1m;

            //prediction = decisionTreeModel.Predict(prediction);
            prediction = model.Predict(prediction);

            //prediction = decisionTreeModel.Predict(data[149]);
            prediction = model.Predict(data[149]);

            LearningModel learned;
            IModel learnedModel;
            double accuracy;
            //  learned = Learner.Learn(data, 0.80, 1, decisionTreeGenerator);
            //  learnedModel = learned.Model;
            //  accuracy = learned.Accuracy;

            learned = Learner.Learn(data, 0.10, 10, generator);
            learnedModel = learned.Model;
            accuracy = learned.Accuracy;
         }
      }
   }
}
