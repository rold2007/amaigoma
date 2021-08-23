using Amaigoma;
using MathNet.Numerics.LinearAlgebra;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using DataTransformer = System.Converter<System.Collections.Generic.IList<double>, System.Collections.Generic.IList<double>>;

namespace AmaigomaConsole
{
   internal class TempDataTransformer
   {
      public TempDataTransformer()
      {
      }

      public IList<double> ConvertAll(IList<double> list)
      {
         List<double> features = new List<double>();

         const int sizeX = 24;
         const int sizeY = 24;
         const int windowSize = 3;

         for (int y = 0; y < sizeY - windowSize; y += windowSize)
         {
            for (int x = 0; x < sizeX - windowSize; x += windowSize)
            {
               double sum = 0;

               for (int j = 0; j < windowSize; j++)
               {
                  int offsetStart = x + ((y + j) * sizeX);

                  for (int i = 0; i < windowSize; i++)
                  {
                     sum += list[offsetStart + i];
                  }
               }

               features.Add(sum / (windowSize * windowSize));
            }
         }

         return features;
      }
   }

   class Program
   {
      static void Main()
      {
         List<Image<L8>> sourceImages = new List<Image<L8>>();

         sourceImages.Add(Image.Load<L8>(@"Images\FS18800114.2.11-a2-427w-c32\a\a.png"));

         foreach (string filename in Directory.EnumerateFiles(@"Images\FS18800114.2.11-a2-427w-c32\Noise\"))
         {
            sourceImages.Add(Image.Load<L8>(filename));
         }

         foreach (string filename in Directory.EnumerateFiles(@"Images\FS18800114.2.11-a2-427w-c32\NotA\"))
         {
            sourceImages.Add(Image.Load<L8>(filename));
         }

         sourceImages.Add(Image.Load<L8>(@"Images\FS18800114.2.11-a2-427w-c32\r\arrived.png"));

         PakiraDecisionTreeGenerator pakiraGenerator = new PakiraDecisionTreeGenerator();
         const int featureWindowSize = 24;
         const int featureCount = featureWindowSize * featureWindowSize;
         int sampleCount = sourceImages.Count;
         Matrix<double> samples = Matrix<double>.Build.Dense(sampleCount, featureCount);
         Vector<double> labels = Vector<double>.Build.Dense(sampleCount);

         labels[0] = 42;

         for (int i = 1; i < sampleCount; i++)
         {
            labels[i] = -1;
         }

         labels[labels.Count - 1] = 2;

         int sampleIndex = 0;
         Span<L8> imagePixels;

         foreach (Image<L8> image in sourceImages)
         {
            image.TryGetSinglePixelSpan(out imagePixels).ShouldBeTrue();

            for (int pixelIndex = 0; pixelIndex < imagePixels.Length; pixelIndex++)
            {
               samples.At(sampleIndex, pixelIndex, imagePixels[pixelIndex].PackedValue);
            }

            sampleIndex++;
         }

         PassThroughTransformer passThroughTransformer = new PassThroughTransformer();
         TempDataTransformer tempDataTransformer = new TempDataTransformer();

         DataTransformer dataTransformers = null;

         dataTransformers += passThroughTransformer.ConvertAll;
         dataTransformers += tempDataTransformer.ConvertAll;
         //dataTransformers += passThroughTransformer.ConvertAll;

         pakiraGenerator.MinimumSampleCount = 10;
         //pakiraGenerator.MinimumSampleCount = 50;
         //pakiraGenerator.MinimumSampleCount = 100;
         //pakiraGenerator.MinimumSampleCount = 200;
         //pakiraGenerator.MinimumSampleCount = 500;
         //pakiraGenerator.MinimumSampleCount = 1000;
         //pakiraGenerator.MinimumSampleCount = 10000;

         pakiraGenerator.CertaintyScore = 0.50;
         //pakiraGenerator.CertaintyScore = 0.75;
         //pakiraGenerator.CertaintyScore = 0.95;
         //pakiraGenerator.CertaintyScore = 1.0;
         //pakiraGenerator.CertaintyScore = 1.1;
         //pakiraGenerator.CertaintyScore = 10000.1;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(PakiraTree.Empty, dataTransformers, samples.Row(0));

         //pakiraGenerator.DataTransformers = dataTransformers;

         pakiraGenerator.Generate(pakiraDecisionTreeModel, samples.EnumerateRows(), labels);

         double resultClass;

         Image<L8> fullTextImage = Image.Load<L8>(@"Images\FS18800114.2.11-a2-427w-c32.png");
         CropProcessor cropProcessor;
         string folder;

         for (int y = 0; y < fullTextImage.Height - featureWindowSize; y++)
         {
            for (int x = 0; x < fullTextImage.Width - featureWindowSize; x++)
            {
               cropProcessor = new CropProcessor(new Rectangle(x, y, featureWindowSize, featureWindowSize), fullTextImage.Size());

               ICloningImageProcessor<L8> cloningImageProcessor = cropProcessor.CreatePixelSpecificCloningProcessor(Configuration.Default, fullTextImage, new Rectangle(x, y, featureWindowSize, featureWindowSize));

               Image<L8> croppedImage = cloningImageProcessor.CloneAndExecute();

               croppedImage.TryGetSinglePixelSpan(out imagePixels).ShouldBeTrue();

               Vector<double> croppedSample = Vector<double>.Build.Dense(imagePixels.Length);

               for (int pixelIndex = 0; pixelIndex < imagePixels.Length; pixelIndex++)
               {
                  croppedSample.At(pixelIndex, imagePixels[pixelIndex].PackedValue);
               }

               SabotenCache croppedSampleCache = new SabotenCache(croppedSample);

               resultClass = pakiraDecisionTreeModel.Predict(croppedSampleCache);

               if (resultClass == 42)
               {
                  string resultClassString = resultClass.ToString();

                  folder = "c:\\!\\argh\\" + resultClassString;
                  System.IO.Directory.CreateDirectory(folder);
                  string path = folder + "\\" + x.ToString() + "x" + y.ToString() + ".png";

                  croppedImage.SaveAsPng(path);
               }
            }
         }


         //foreach (SabotenCache dataDistributionSampleCache in pakiraDecisionTreeModel.DataDistributionSamplesCache)
         //{
         //   resultClass = pakiraDecisionTreeModel.Predict(dataDistributionSampleCache);

         //   if (resultClass == 42)
         //   {
         //      string resultClassString = resultClass.ToString();

         //      byte[] grayscaleBytes = Array.ConvertAll<double, byte>(dataDistributionSampleCache.Data.ToArray(), x => Convert.ToByte(x));

         //      using (Image<L8> image = Image.LoadPixelData<L8>(grayscaleBytes, 24, 24))
         //      {
         //         folder = "c:\\!\\" + resultClassString;
         //         string path = folder + "\\" + System.IO.Path.GetRandomFileName() + ".png";

         //         System.IO.Directory.CreateDirectory(folder);

         //         image.SaveAsPng(path);
         //      }
         //   }
         //}
      }

      static void MainOld()
      {
         //List<Property> features = new List<Property>();

         for (int i = 0; i < 3; i++)
         {
            //SumFeature sumFeature = new SumFeature(i, i + 1)
            //{
            //   Name = "Sum" + i.ToString(),
            //   Type = typeof(System.Double)
            //};

            //features.Add(sumFeature);

            //ProductFeature productFeature = new ProductFeature(i, i + 1)
            //{
            //   Name = "Product" + i.ToString(),
            //   Type = typeof(System.Double)
            //};

            //features.Add(productFeature);
         }

         for (int i = 0; i < 1; i++)
         {
            //RandomFeature randomFeature = new RandomFeature()
            //{
            //   Name = "Random" + i.ToString(),
            //   Type = typeof(System.Double)
            //};

            //features.Add(randomFeature);
         }

         //PakiraDescriptor description = PakiraDescriptor.Create<Iris>();
         //PakiraDescriptor fluentDescriptor = PakiraDescriptor.New(typeof(Iris))
         //                                          .With("SepalLength").As(typeof(decimal))
         //                                          .With("SepalWidth").As(typeof(double))
         //                                          .With("PetalLength").As(typeof(decimal))
         //                                          .With("PetalWidth").As(typeof(int))
         //                                          .Learn("Class").As(typeof(string));

         //features.AddRange(fluentDescriptor.Features);

         //fluentDescriptor.Features = features.ToArray();

         //Console.WriteLine(description);
         //Console.WriteLine(fluentDescriptor);

         //Iris[] data = Iris.Load();
         //List<Generator> generators = new List<Generator>();

         const int sampleCount = 400000;
         //const int minimumSampleCount = /*500*/100;
         //Iris[] samples = new Iris[sampleCount];

         Parallel.For(0, sampleCount, i =>
         //for (int i = 0; i < sampleCount; i++)
         {
            //decimal sepalLength = Convert.ToDecimal(MySampling.GetUniform(0.1, 10.0));
            //decimal sepalWidth = Convert.ToDecimal(MySampling.GetUniform(0.1, 6.0));
            //decimal petalLength = Convert.ToDecimal(MySampling.GetUniform(0.1, 8.0));
            //decimal petalWidth = Convert.ToDecimal(MySampling.GetUniform(0.1, 3.0));

            //System.Diagnostics.Debug.WriteLine(sepalLength.ToString() + ";" + sepalWidth.ToString());

            // samples.Add(new Iris { SepalLength = sepalLength, SepalWidth = sepalWidth, PetalLength = petalLength, PetalWidth = petalWidth, Class = string.Empty });
            //samples[i] = new Iris { SepalLength = sepalLength, SepalWidth = sepalWidth, PetalLength = petalLength, PetalWidth = petalWidth, Class = string.Empty };
         }
          );

         //PakiraGenerator pakiraGenerator = new PakiraGenerator(samples, minimumSampleCount);
         //PakiraModel pakiraModel = null;
         //List<Iris> trainingSet = new List<Iris>() { data[0], data[50], data[100] };
         //List<Iris> trainingSamples = new List<Iris>();
         //List<Iris> testSamples = new List<Iris>();

         //for (int dataindex = 1; dataindex < data.Count() / 3; dataindex++)
         //{
         //   if (dataindex < 25)
         //   {
         //      trainingSamples.Add(data[dataindex]);
         //      trainingSamples.Add(data[dataindex + 50]);
         //      trainingSamples.Add(data[dataindex + 100]);
         //   }
         //   else
         //   {
         //      testSamples.Add(data[dataindex]);
         //      testSamples.Add(data[dataindex + 50]);
         //      testSamples.Add(data[dataindex + 100]);
         //   }
         //}

         //pakiraModel = pakiraGenerator.Generate(new List<Iris>() { data[0], data[50], data[100] });
         //pakiraModel = pakiraGenerator.Generate(new List<Iris>() { data[0], data[1], data[50], data[51], data[100], data[101] });
         //pakiraModel = pakiraGenerator.Generate(new List<Iris>() { data[0], data[50], data[51], data[52], data[53], data[54], data[100] });
         //pakiraModel = pakiraGenerator.Generate(new List<Iris>() { data[0], data[1], data[2], data[50], data[51], data[52], data[100], data[101], data[102] });

         bool trainingSamplesCountIncreased = true;
         //int previousSamplesCount = -1;
         int trainingRound = 0;

         while (trainingSamplesCountIncreased)
         {
            Console.WriteLine("Training " + trainingRound.ToString());

            //pakiraModel = pakiraGenerator.Generate(trainingSet);

            //Console.WriteLine("Model " + pakiraModel.ToString());

            //Dictionary<Node, int> failedNodes = new Dictionary<Node, int>();

            for (int swapRootNode = 0; swapRootNode < 2; swapRootNode++)
            {
               // int labelCount = (pakiraModel.Descriptor.Label as StringProperty).Dictionary.Count();
               ConfusionMatrix trainingSamplesConfusionMatrix = new ConfusionMatrix(0/*labelCount*/);
               ConfusionMatrix testSamplesConfusionMatrix = new ConfusionMatrix(0/*labelCount*/);

               //for (int i = 0; i < trainingSamples.Count(); i++)
               {
                  /*
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
                  */
                  //var inEdges = pakiraModel.Tree.GetInEdges(predictionNode).ToList();
                  //var parents = pakiraModel.Tree.GetParents(predictionNode).ToList();
               }

               //for (int i = 0; i < testSamples.Count(); i++)
               {
                  /*
                  (Matrix, Vector) valueTuple = new List<IEnumerable<double>>() { pakiraGenerator.Descriptor.Convert(testSamples[i], true) }.ToExamples();
                  Node predictionNode = pakiraModel.Predict(valueTuple.Item1.Row(0));
                  int currentSampleClass = (int)valueTuple.Item2[0];
                  int predictedClass = (int)predictionNode.Value;

                  testSamplesConfusionMatrix.AddPrediction(currentSampleClass, predictedClass);

                  if (currentSampleClass != predictedClass)
                  {
                     //Console.WriteLine("Expected " + currentSampleClass.ToString() + " but predicted " + predictedClass.ToString());
                  }
                  */

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

               if (swapRootNode == 0)
               {
                  //IEdge[] edges = pakiraModel.Tree.GetOutEdges(pakiraModel.Tree.Root).ToArray();

                  // Swap values using tuples
                  //(edges[0].ChildId, edges[1].ChildId) = (edges[1].ChildId, edges[0].ChildId);
               }
            }

            SortedSet<int> sortedDataIndices = new SortedSet<int>();

            // Transfer some samples from the training samples to the training set
            //foreach (int dataIndex in failedNodes.Values)
            //{
            //   trainingSet.Add(trainingSamples[dataIndex]);
            //   sortedDataIndices.Add(dataIndex);
            //}

            //foreach (int dataIndex in sortedDataIndices.Reverse())
            //{
            //   trainingSamples.RemoveAt(dataIndex);
            //}

            //trainingSamplesCountIncreased = (previousSamplesCount != trainingSamples.Count());
            //previousSamplesCount = trainingSamples.Count();

            trainingRound++;
         }


         int generatorIndex = 0;

         //foreach (Generator generator in generators)
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

            //LearningModel learned;
            //IModel learnedModel;
            //double accuracy;
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
               //learned = Learner.Learn(data, 0.10, 1, generator);
               //learnedModel = learned.Model;
               //accuracy = learned.Accuracy;

               //minAccuracy = Math.Min(minAccuracy, accuracy);
               //maxAccuracy = Math.Max(maxAccuracy, accuracy);
               //sumAccuracy += accuracy;

               //Console.WriteLine(accuracy.ToString());
               //Console.WriteLine("Model " + learnedModel.ToString());
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
