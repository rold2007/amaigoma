namespace Amaigoma
{
   using System;
   using numl;
   using numl.Math;
   using numl.Math.LinearAlgebra;
   using numl.Model;
   using numl.Supervised;
   using numl.Supervised.NaiveBayes;

   public class PakiraGenerator : Generator
   {
      public PakiraGenerator()
      {
      }

      public override IModel Generate(Matrix x, Vector y)
      {
         if (Descriptor == null)
            throw new InvalidOperationException("Cannot build naive bayes model without type knowledge!");

         // create answer probabilities
         if (!Descriptor.Label.Discrete)
            throw new InvalidOperationException("Need to use regression for non-discrete labels!");

         NaiveBayesModel naiveBayesModel = new NaiveBayesModel();

         naiveBayesModel.Descriptor = this.Descriptor;

         Statistic[] statistics = new Statistic[1];

         statistics[0] = new Statistic();

         statistics[0].Probability = 100.0;

         statistics[0].Conditionals = new Measure[4];

         for (int i = 0; i < x.Cols; i++)
         {
            statistics[0].Conditionals[i] = new Measure();

            statistics[0].Conditionals[i].Probabilities = new Statistic[1];
            statistics[0].Conditionals[i].Probabilities[0] = new Statistic();

            statistics[0].Conditionals[i].Probabilities[0].X = new Range();

            statistics[0].Conditionals[i].Probabilities[0].X.Min = 0.0;
            statistics[0].Conditionals[i].Probabilities[0].X.Max = 0.0;

            statistics[0].Conditionals[i].Probabilities[0].Conditionals = new Measure[1];
            // statistics[0].Conditionals[i].Probabilities[0].Conditionals[0].
         }

         statistics[0].X = new Range();

         statistics[0].X.Min = 0.0;
         statistics[0].X.Max = 0.0;

         Measure root = new Measure
         {
            Discrete = true,
            Label = Descriptor.Label.Name,
            Probabilities = statistics
         };

         naiveBayesModel.Root = root;

         return naiveBayesModel;
      }
   }
}