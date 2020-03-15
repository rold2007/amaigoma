namespace Amaigoma
{
   using numl.Math.LinearAlgebra;
   using numl.Serialization;
   using System.IO;
   using System.Text;

   /// <summary>A model.</summary>
   public abstract class BaseModel
   {
      /// <summary>Predicts the given o.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>An object.</returns>
      public abstract double Predict(Vector y);

      /// <summary>Predicts the given o.</summary>
      /// <tparam name="T">Generic type parameter.</tparam>
      /// <param name="o">The object to process.</param>
      /// <returns>A T.</returns>
      public T Predict<T>(T o)
      {
         return (T)Predict((object)o);
      }

      // ----- saving stuff
      /// <summary>Model persistence.</summary>
      /// <param name="file">The file to load.</param>
      public virtual void Save(string file)
      {
         if (File.Exists(file)) File.Delete(file);
         using (var fs = new FileStream(file, FileMode.CreateNew))
         using (var f = new StreamWriter(fs))
            new JsonWriter(f).Write(this);
      }

      /// <summary>Converts this object to json.</summary>
      /// <returns>This object as a string.</returns>
      public virtual string ToJson()
      {
         StringBuilder sb = new StringBuilder();
         using (StringWriter sw = new StringWriter(sb))
            new JsonWriter(sw).Write(this);
         return sb.ToString();
      }
   }
}
