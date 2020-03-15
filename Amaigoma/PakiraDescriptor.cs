namespace Amaigoma
{
   using numl.Math.LinearAlgebra;
   using numl.Model;
   using numl.Utils;
   using System;
   using System.Collections;
   using System.Collections.Generic;
   using System.IO;
   using System.Linq;
   using System.Linq.Expressions;
   using System.Reflection;
   using System.Text;

   /// <summary>
   /// This class is designed to describe the underlying types that will be used in the machine
   /// learning process. Any machine learning process requires a set of <see cref="Features"/> that
   /// will be used to discriminate the <see cref="Label"/>. The <see cref="Label"/> itself is the
   /// target element that the machine learning algorithms learn to predict.
   /// </summary>
   public class PakiraDescriptor
   {
      /// <summary>Default constructor.</summary>
      public PakiraDescriptor()
      {
         Name = "";
      }

      /// <summary>Descriptor name.</summary>
      /// <value>The name.</value>
      public string Name { get; set; }
      /// <summary>
      /// Set of features used to discriminate or learn about the <see cref="Label"/>.
      /// </summary>
      /// <value>The features.</value>
      public Property[] Features { get; set; }
      /// <summary>Target property that is the target of machine learning.</summary>
      /// <value>The label.</value>
      public Property Label { get; set; }
      /// <summary>Length of the vector.</summary>
      private int _vectorLength = -1;
      /// <summary>
      /// Total feature count The number of features does not necessarily equal the number of
      /// <see cref="Features"/> given that there might exist multi-valued features.
      /// </summary>
      /// <value>The length of the vector.</value>
      public int VectorLength
      {
         get
         {
            return _vectorLength;
         }
      }
      /// <summary>Base type of object being described. This could also be null.</summary>
      /// <value>The type.</value>
      public Type Type { get; set; }
      /// <summary>Gets related property given its offset within the vector representation.</summary>
      /// <exception cref="IndexOutOfRangeException">Thrown when the index is outside the required
      /// range.</exception>
      /// <param name="i">Vector Index.</param>
      /// <returns>Associated Feature.</returns>
      public Property At(int i)
      {
         if (i < 0 || i > VectorLength)
            throw new IndexOutOfRangeException(string.Format("{0} falls outside of the appropriate range", i));

         var q = (from p in Features
                  where i >= p.Start && i < p.Start + p.Length
                  select p);

         return q.First();
      }
      /// <summary>
      /// Gets related property column name given its offset within the vector representation.
      /// </summary>
      /// <param name="i">Vector Index.</param>
      /// <returns>Associated Property Name.</returns>
      public string ColumnAt(int i)
      {
         var prop = At(i);
         var offset = i - prop.Start;
         var col = prop.GetColumns().ElementAt(offset);
         return col;
      }
      /// <summary>
      /// Converts a given example into a lazy list of doubles in preparation for vector conversion
      /// (both features and corresponding label)
      /// </summary>
      /// <param name="item">Example.</param>
      /// <returns>Lazy List of doubles.</returns>
      public IEnumerable<double> Convert(object item)
      {
         return Convert(item, true);
      }
      /// <summary>
      /// Converts a given example into a lazy list of doubles in preparation for vector conversion.
      /// </summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="item">Example.</param>
      /// <param name="withLabel">Should convert label as well.</param>
      /// <returns>Lazy List of doubles.</returns>
      public IEnumerable<double> Convert(object item, bool withLabel)
      {
         if (Features.Length == 0)
            throw new InvalidOperationException("Cannot convert item with an empty Feature set.");

         for (int i = 0; i < Features.Length; i++)
         {
            // current feature
            var feature = Features[i];

            // pre-process item
            feature.PreProcess(item);

            // start position
            if (feature.Start < 0)
               feature.Start = i == 0 ? 0 : Features[i - 1].Start + Features[i - 1].Length;

            // retrieve item
            var o = Ject.Get(item, feature.Name);

            // convert item
            foreach (double val in feature.Convert(o))
               yield return val;

            // post-process item
            feature.PostProcess(item);
         }

         // convert label (if available)
         if (Label != null && withLabel)
            foreach (double val in Label.Convert(Ject.Get(item, Label.Name)))
               yield return val;

      }
      /// <summary>Converts a list of examples into a lazy double list of doubles.</summary>
      /// <param name="items">Examples.</param>
      /// <param name="withLabels">True to include labels, otherwise False</param>
      /// <returns>Lazy double enumerable of doubles.</returns>
      public IEnumerable<IEnumerable<double>> Convert(IEnumerable<object> items, bool withLabels = true, bool updateLabels = true)
      {
         // Pre processing items
         foreach (Property feature in Features)
            feature.PreProcess(items);

         if (Label != null && updateLabels)
            Label.PreProcess(items);

         // convert items
         foreach (object o in items)
            yield return Convert(o, withLabels);

         // Post processing items
         foreach (Property feature in Features)
            feature.PostProcess(items);

         if (Label != null && updateLabels)
            Label.PostProcess(items);
      }
      /// <summary>Pretty printed descriptor.</summary>
      /// <returns>Pretty printed string.</returns>
      public override string ToString()
      {
         StringBuilder sb = new StringBuilder();
         sb.AppendLine($"Descriptor ({Type?.Name ?? Name}) {{");
         for (int i = 0; i < Features.Length; i++)
            sb.AppendLine($"   {Features[i]}");
         if (Label != null)
            sb.AppendLine($"  *{Label}");

         sb.AppendLine("}");
         return sb.ToString();
      }

      //---- Creational

      /// <summary>
      /// Creates a descriptor based upon a marked up concrete class.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <returns>Descriptor.</returns>
      public static PakiraDescriptor Create<T>()
         where T : class
      {

         return Create(typeof(T));
      }
      /// <summary>Creates a descriptor based upon a marked up concrete type.</summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="t">Class Type.</param>
      /// <returns>Descriptor.</returns>
      public static PakiraDescriptor Create(Type t)
      {
         if (!t.GetTypeInfo().IsClass)
            throw new InvalidOperationException("Can only work with class types");

         List<Property> features = new List<Property>();
         Property label = null;

         foreach (PropertyInfo property in t.GetProperties())
         {
            var item = property.GetCustomAttributes(typeof(NumlAttribute), false);

            if (item.Count() == 1)
            {
               var attrib = (NumlAttribute)item.First();

               // generate appropriate property from attribute
               Property p = attrib.GenerateProperty(property);

               // feature
               if (attrib.GetType().GetTypeInfo().IsSubclassOf(typeof(FeatureAttribute)) ||
                  attrib is FeatureAttribute)
                  features.Add(p);
               // label
               else if (attrib.GetType().GetTypeInfo().IsSubclassOf(typeof(LabelAttribute)) ||
                  attrib is LabelAttribute)
               {
                  if (label != null)
                     throw new InvalidOperationException("Cannot have multiple labels in a class");
                  label = p;
               }
            }
         }

         return new PakiraDescriptor
         {
            Label = label,
            Type = t,
         };
      }

      /// <summary>
      /// Creates a new descriptor using a fluent approach. This initial descriptor is worthless
      /// without adding features.
      /// </summary>
      /// <returns>Empty Descriptor.</returns>
      public static PakiraDescriptor New()
      {
         return new PakiraDescriptor() { Features = new Property[] { } };
      }
      /// <summary>
      /// Creates a new descriptor using a fluent approach. This initial descriptor is worthless
      /// without adding features.
      /// </summary>
      /// <param name="type">Type mapping.</param>
      /// <returns>A Descriptor.</returns>
      public static PakiraDescriptor New(Type type)
      {
         return new PakiraDescriptor() { Type = type, Features = new Property[] { } };
      }

      /// <summary>
      /// Creates a new descriptor using a strongly typed fluent approach. This initial descriptor is
      /// worthless without adding features.
      /// </summary>
      /// <typeparam name="T">Source Object Type</typeparam>
      /// <param name="name">Desired Descriptor Name.</param>
      /// <returns>Empty descriptor</returns>
      public static Descriptor<T> For<T>(string name)
      {
         return new Descriptor<T>() { Name = name, Type = typeof(T), Features = new Property[] { } };
      }

      /// <summary>
      /// Load a descriptor from a stream.
      /// </summary>
      /// <param name="stream">Stream.</param>
      /// <returns>Descriptor.</returns>
      /// <exception cref="System.NotImplementedException"></exception>
      public static PakiraDescriptor Load(Stream stream)
      {
         throw new NotImplementedException();
      }
      /// <summary>Adds a new feature to descriptor.</summary>
      /// <param name="name">Name of feature (must match property name or dictionary key)</param>
      /// <returns>method for describing feature.</returns>
      public PakiraDescriptorProperty With(string name)
      {
         return new PakiraDescriptorProperty(this, name, false);
      }
      /// <summary>Adds (or replaces) a label to the descriptor.</summary>
      /// <param name="name">Name of label (must match property name or dictionary key)</param>
      /// <returns>A DescriptorProperty.</returns>
      public PakiraDescriptorProperty Learn(string name)
      {
         return new PakiraDescriptorProperty(this, name, true);
      }

      /// <summary>
      /// Equality test
      /// </summary>
      /// <param name="obj">object to compare</param>
      /// <returns>equality</returns>
      public override bool Equals(object obj)
      {
         if (obj is PakiraDescriptor)
         {
            var d = obj as PakiraDescriptor;
            if (Features.Length == d.Features.Length)
            {
               for (int i = 0; i < Features.Length; i++)
                  if (!Features[i].Equals(d.Features[i]))
                     return false;

               if ((Label != null &&
                  d.Label != null))
                  return Label.Equals(d.Label) &&
                        Name == d.Name &&
                        Type == d.Type &&
                        VectorLength == d.VectorLength &&
                        Features.Length == d.Features.Length;
               else
                  return Label == null &&
                        d.Label == null &&
                        Name == d.Name &&
                        Type == d.Type &&
                        VectorLength == d.VectorLength &&
                        Features.Length == d.Features.Length;
            }
         }
         return false;
      }

      /// <summary>
      /// Return hash
      /// </summary>
      /// <returns>hash</returns>
      public override int GetHashCode()
      {
         return base.GetHashCode();
      }
   }


   /// <summary>
   /// Class Descriptor.
   /// </summary>
   /// <typeparam name="T"></typeparam>
   public class Descriptor<T> : PakiraDescriptor
   {
      /// <summary>Initializes a new instance of the Descriptor class.</summary>
      public Descriptor()
      {
         Type = typeof(T);
         Features = new Property[] { };
      }
      /// <summary>Adds a property to 'label'.</summary>
      /// <param name="p">The Property to process.</param>
      /// <param name="label">true to label.</param>
      private void AddProperty(Property p, bool label)
      {
         if (label)
            Label = p;
         else
         {
            var features = new List<Property>(Features ?? new Property[] { });
            features.Add(p);
            Features = features.ToArray();
         }
      }
      /// <summary>Gets property information.</summary>
      /// <tparam name="K">Generic type parameter.</tparam>
      /// <param name="property">The property.</param>
      /// <returns>The property information.</returns>
      private static PropertyInfo GetPropertyInfo<K>(Expression<Func<T, K>> property)
      {
         PropertyInfo propertyInfo = null;
         if (property.Body is MemberExpression)
            propertyInfo = (property.Body as MemberExpression).Member as PropertyInfo;
         else
            propertyInfo = (((UnaryExpression)property.Body).Operand as MemberExpression).Member as PropertyInfo;

         return propertyInfo;
      }
      /// <summary>Withs the given property.</summary>
      /// <param name="property">The property.</param>
      /// <returns>A Descriptor&lt;T&gt;</returns>
      public Descriptor<T> With(Expression<Func<T, Object>> property)
      {
         var pi = GetPropertyInfo<Object>(property);
         AddProperty(TypeHelpers.GenerateFeature(pi.PropertyType, pi.Name), false);
         return this;
      }
      /// <summary>With string.</summary>
      /// <param name="property">The property.</param>
      /// <param name="splitType">Type of the split.</param>
      /// <param name="separator">(Optional) the separator.</param>
      /// <param name="asEnum">(Optional) true to as enum.</param>
      /// <param name="exclusions">(Optional) base 64 content string of the exclusions.</param>
      /// <returns>A Descriptor&lt;T&gt;</returns>
      public Descriptor<T> WithString(Expression<Func<T, string>> property, StringSplitType splitType, string separator = " ", bool asEnum = false, string exclusions = null)
      {
         var pi = GetPropertyInfo<string>(property);
         StringProperty p = new StringProperty();
         p.Name = pi.Name;
         p.SplitType = splitType;
         p.Separator = separator;
         p.ImportExclusions(exclusions);
         p.AsEnum = asEnum;
         AddProperty(p, false);
         return this;
      }
      /// <summary>With date time.</summary>
      /// <param name="property">The property.</param>
      /// <param name="features">The features.</param>
      /// <returns>A Descriptor&lt;T&gt;</returns>
      public Descriptor<T> WithDateTime(Expression<Func<T, DateTime>> property, DateTimeFeature features)
      {
         var pi = GetPropertyInfo<DateTime>(property);
         var p = new DateTimeProperty(features)
         {
            Discrete = true,
            Name = pi.Name
         };

         AddProperty(p, false);
         return this;
      }
      /// <summary>With date time.</summary>
      /// <param name="property">The property.</param>
      /// <param name="portion">The portion.</param>
      /// <returns>A Descriptor&lt;T&gt;</returns>
      public Descriptor<T> WithDateTime(Expression<Func<T, DateTime>> property, DatePortion portion)
      {
         var pi = GetPropertyInfo<DateTime>(property);
         var p = new DateTimeProperty(portion)
         {
            Discrete = true,
            Name = pi.Name
         };

         AddProperty(p, false);
         return this;
      }
      /// <summary>With guid.</summary>
      /// <param name="property">The property.</param>
      /// <returns>A Descriptor&lt;T&gt;</returns>
      public Descriptor<T> WithGuid(Expression<Func<T, Guid>> property)
      {
         var pi = GetPropertyInfo<Guid>(property);
         var p = new GuidProperty()
         {
            Discrete = true,
            Name = pi.Name
         };

         AddProperty(p, false);
         return this;
      }
      /// <summary>With enumerable.</summary>
      /// <param name="property">The property.</param>
      /// <param name="length">The length.</param>
      /// <returns>A Descriptor&lt;T&gt;</returns>
      public Descriptor<T> WithEnumerable(Expression<Func<T, IEnumerable>> property, int length)
      {
         var pi = GetPropertyInfo<IEnumerable>(property);
         var p = new EnumerableProperty(length)
         {
            Name = pi.Name,
            Discrete = false
         };

         AddProperty(p, false);
         return this;
      }
      /// <summary>Learns the given property.</summary>
      /// <param name="property">The property.</param>
      /// <returns>A Descriptor&lt;T&gt;</returns>
      public Descriptor<T> Learn(Expression<Func<T, Object>> property)
      {
         var pi = GetPropertyInfo(property);
         AddProperty(TypeHelpers.GenerateLabel(pi.PropertyType, pi.Name), true);
         return this;
      }
   }
}