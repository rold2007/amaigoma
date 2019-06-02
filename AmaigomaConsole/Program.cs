namespace AmaigomaConsole
{
   using Amaigoma;
   using ExtensionMethods;
   using numl;
   using numl.Data;
   using numl.Math.LinearAlgebra;
   using numl.Model;
   using numl.Supervised;
   using numl.Supervised.DecisionTree;
   using numl.Tests.Data;
   using Shouldly;
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using System.Threading.Tasks;

   class Program
   {
      static void Main()
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

         for (int i = 0; i < 1; i++)
         {
            RandomFeature randomFeature = new RandomFeature()
            {
               Name = "Random" + i.ToString(),
               Type = typeof(System.Double)
            };

            features.Add(randomFeature);
         }

         PakiraDescriptor description = PakiraDescriptor.Create<Iris>();
         PakiraDescriptor fluentDescriptor = PakiraDescriptor.New(typeof(Iris))
                                                   .With("SepalLength").As(typeof(decimal))
                                                   .With("SepalWidth").As(typeof(double))
                                                   .With("PetalLength").As(typeof(decimal))
                                                   .With("PetalWidth").As(typeof(int))
                                                   .Learn("Class").As(typeof(string));

         features.AddRange(fluentDescriptor.Features);

         fluentDescriptor.Features = features.ToArray();

         Console.WriteLine(description);
         Console.WriteLine(fluentDescriptor);

         Iris[] data = Iris.Load();
         List<Generator> generators = new List<Generator>();

         const int sampleCount = 400000;
         const int minimumSampleCount = /*500*/100;
         Iris[] samples = new Iris[sampleCount];

         Parallel.For(0, sampleCount, i =>
         //for (int i = 0; i < sampleCount; i++)
         {
            decimal sepalLength = Convert.ToDecimal(MySampling.GetUniform(0.1, 10.0));
            decimal sepalWidth = Convert.ToDecimal(MySampling.GetUniform(0.1, 6.0));
            decimal petalLength = Convert.ToDecimal(MySampling.GetUniform(0.1, 8.0));
            decimal petalWidth = Convert.ToDecimal(MySampling.GetUniform(0.1, 3.0));

            //System.Diagnostics.Debug.WriteLine(sepalLength.ToString() + ";" + sepalWidth.ToString());

            // samples.Add(new Iris { SepalLength = sepalLength, SepalWidth = sepalWidth, PetalLength = petalLength, PetalWidth = petalWidth, Class = string.Empty });
            samples[i] = new Iris { SepalLength = sepalLength, SepalWidth = sepalWidth, PetalLength = petalLength, PetalWidth = petalWidth, Class = string.Empty };
         }
          );

         PakiraGenerator pakiraGenerator = new PakiraGenerator(fluentDescriptor, samples, PakiraGenerator.UNKNOWN_CLASS_INDEX, minimumSampleCount);
         PakiraModel pakiraModel;
         List<Iris> trainingSet = new List<Iris>() { data[0], data[50], data[100] };
         List<Iris> trainingSamples = new List<Iris>();
         List<Iris> testSamples = new List<Iris>();

         for (int dataindex = 1; dataindex < data.Count() / 3; dataindex++)
         {
            if (dataindex < 25)
            {
               trainingSamples.Add(data[dataindex]);
               trainingSamples.Add(data[dataindex + 50]);
               trainingSamples.Add(data[dataindex + 100]);
            }
            else
            {
               testSamples.Add(data[dataindex]);
               testSamples.Add(data[dataindex + 50]);
               testSamples.Add(data[dataindex + 100]);
            }
         }

         //pakiraModel = pakiraGenerator.Generate(new List<Iris>() { data[0], data[50], data[100] });
         //pakiraModel = pakiraGenerator.Generate(new List<Iris>() { data[0], data[1], data[50], data[51], data[100], data[101] });
         //pakiraModel = pakiraGenerator.Generate(new List<Iris>() { data[0], data[50], data[51], data[52], data[53], data[54], data[100] });
         //pakiraModel = pakiraGenerator.Generate(new List<Iris>() { data[0], data[1], data[2], data[50], data[51], data[52], data[100], data[101], data[102] });

         bool trainingSamplesCountIncreased = true;
         int previousSamplesCount = -1;
         int trainingRound = 0;

         while (trainingSamplesCountIncreased)
         {
            Console.WriteLine("Training " + trainingRound.ToString());

            pakiraModel = pakiraGenerator.Generate(trainingSet);

            Console.WriteLine("Model " + pakiraModel.ToString());

            Dictionary<Node, int> failedNodes = new Dictionary<Node, int>();

            for (int swapRootNode = 0; swapRootNode < 2; swapRootNode++)
            {
               int labelCount = (pakiraModel.Descriptor.Label as StringProperty).Dictionary.Count();
               ConfusionMatrix trainingSamplesConfusionMatrix = new ConfusionMatrix(labelCount);
               ConfusionMatrix testSamplesConfusionMatrix = new ConfusionMatrix(labelCount);

               for (int i = 0; i < trainingSamples.Count(); i++)
               {
                  (Matrix, Vector) valueTuple = new List<IEnumerable<double>>() { pakiraGenerator.Descriptor.Convert(trainingSamples[i], true) }.ToExamples();
                  Node predictionNode = pakiraModel.Predict(valueTuple.Item1.Row(0));
                  int currentSampleClass = (int)valueTuple.Item2[0];
                  int predictedClass = (int)predictionNode.Value;

                  trainingSamplesConfusionMatrix.AddPrediction(currentSampleClass, predictedClass);

                  if (swapRootNode == 0)
                  {
                     if (currentSampleClass != predictedClass)
                     {
                        predictedClass.ShouldNotBe<int>(labelCount + PakiraGenerator.INSUFFICIENT_SAMPLES_CLASS_INDEX);

                        if (failedNodes.ContainsKey(predictionNode))
                        {
                           // For now, we simply keep the first fail found
                           // But it would probably be better to keep only the worst fail found
                           // The worst fail would have the strongest bad prediction of the last node

                           //(Matrix, Vector) previousFailValueTuple = new List<IEnumerable<double>>() { pakiraGenerator.Descriptor.Convert(trainingSamples[failedNodes[predictionNode]], true) }.ToExamples();
                        }
                        else
                        {
                           failedNodes[predictionNode] = i;
                        }
                     }
                  }

                  //var inEdges = pakiraModel.Tree.GetInEdges(predictionNode).ToList();
                  //var parents = pakiraModel.Tree.GetParents(predictionNode).ToList();
               }

               for (int i = 0; i < testSamples.Count(); i++)
               {
                  (Matrix, Vector) valueTuple = new List<IEnumerable<double>>() { pakiraGenerator.Descriptor.Convert(testSamples[i], true) }.ToExamples();
                  Node predictionNode = pakiraModel.Predict(valueTuple.Item1.Row(0));
                  int currentSampleClass = (int)valueTuple.Item2[0];
                  int predictedClass = (int)predictionNode.Value;

                  testSamplesConfusionMatrix.AddPrediction(currentSampleClass, predictedClass);

                  if (currentSampleClass != predictedClass)
                  {
                     //Console.WriteLine("Expected " + currentSampleClass.ToString() + " but predicted " + predictedClass.ToString());
                  }


                  //var inEdges = pakiraModel.Tree.GetInEdges(predictionNode).ToList();
                  //var parents = pakiraModel.Tree.GetParents(predictionNode).ToList();
               }

               Console.WriteLine();


               List<double> matthewsCorrelationCoefficients = trainingSamplesConfusionMatrix.ComputeMatthewsCorrelationCoefficient();

               Console.WriteLine("Training set Matthews Correlation Coefficients");

               foreach (double matthewsCorrelationCoefficient in matthewsCorrelationCoefficients)
               {
                  Console.WriteLine(matthewsCorrelationCoefficient.ToString() + ";");
               }

               Console.WriteLine();
               Console.WriteLine("Test set Matthews Correlation Coefficients");

               matthewsCorrelationCoefficients = testSamplesConfusionMatrix.ComputeMatthewsCorrelationCoefficient();

               foreach (double matthewsCorrelationCoefficient in matthewsCorrelationCoefficients)
               {
                  Console.WriteLine(matthewsCorrelationCoefficient.ToString() + ";");
               }

               Console.WriteLine();

               if(swapRootNode == 0)
               {
                  IEdge[] edges = pakiraModel.Tree.GetOutEdges(pakiraModel.Tree.Root).ToArray();

                  // Swap values using tuples
                  (edges[0].ChildId, edges[1].ChildId) = (edges[1].ChildId, edges[0].ChildId);
               }
            }

            SortedSet<int> sortedDataIndices = new SortedSet<int>();

            // Transfer some samples from the training samples to the training set
            foreach (int dataIndex in failedNodes.Values)
            {
               trainingSet.Add(trainingSamples[dataIndex]);
               sortedDataIndices.Add(dataIndex);
            }

            foreach (int dataIndex in sortedDataIndices.Reverse())
            {
               trainingSamples.RemoveAt(dataIndex);
            }

            trainingSamplesCountIncreased = (previousSamplesCount != trainingSamples.Count());
            previousSamplesCount = trainingSamples.Count();

            trainingRound++;
         }


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

   public class ConfusionMatrix
   {
      public int LabelCount
      {
         get
         {
            return Matrix.Count();
         }

         private set
         {
            int labelCount = value;

            Matrix.ShouldBeNull();

            Matrix = new List<List<int>>(value);

            for (int i = 0; i < labelCount; i++)
            {
               List<int> matrixLine = new List<int>(labelCount);

               matrixLine.AddRange(Enumerable.Repeat(0, labelCount));

               Matrix.Add(matrixLine);
            }
         }
      }

      private List<List<int>> Matrix
      {
         get;
         set;
      }

      public ConfusionMatrix(int labelCount)
      {
         LabelCount = labelCount;
      }

      public void AddPrediction(int expectedSampleLabel, int predictedSampleLabel)
      {
         Matrix[expectedSampleLabel][predictedSampleLabel]++;
      }

      // Based on https://en.wikipedia.org/wiki/Matthews_correlation_coefficient
      public List<double> ComputeMatthewsCorrelationCoefficient()
      {
         List<double> matthewsCorrelationCoefficients = new List<double>(LabelCount);

         // Compute the Matthews coefficient for each class
         for (int labelIndex = 0; labelIndex < LabelCount; labelIndex++)
         {
            int truePositives = 0;
            int falsePositives = 0;
            int falseNegatives = 0;
            int trueNegatives = 0;

            for (int i = 0; i < LabelCount; i++)
            {
               for (int j = 0; j < LabelCount; j++)
               {
                  if (i == labelIndex)
                  {
                     if (j == labelIndex)
                     {
                        truePositives += Matrix[i][j];
                     }
                     else
                     {
                        falsePositives += Matrix[i][j];
                     }
                  }
                  else
                  {
                     if (i == j)
                     {
                        trueNegatives += Matrix[i][j];
                     }
                     else
                     {
                        falseNegatives += Matrix[i][j];
                     }
                  }
               }
            }

            double matthewsCorrelationCoefficientDenominator = (truePositives + falsePositives) *
                                    (truePositives + falseNegatives) *
                                    (trueNegatives + falsePositives) *
                                    (trueNegatives + falseNegatives);

            if (matthewsCorrelationCoefficientDenominator == 0.0)
            {
               matthewsCorrelationCoefficientDenominator = 1.0;
            }
            else
            {
               matthewsCorrelationCoefficientDenominator = Math.Sqrt(matthewsCorrelationCoefficientDenominator);
            }

            double matthewsCorrelationCoefficient = ((truePositives * trueNegatives) - (falsePositives * falseNegatives)) / matthewsCorrelationCoefficientDenominator;

            matthewsCorrelationCoefficients.Add(matthewsCorrelationCoefficient);
         }

         return matthewsCorrelationCoefficients;
      }
   }
}
