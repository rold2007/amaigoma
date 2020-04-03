namespace Amaigoma
{
   using System;
   using System.Collections.Generic;

   /// <summary>A dictionary helpers.</summary>
   public static class PakiraDictionaryHelpers
   {
      /// <summary>
      /// Adds or updates the value at the specified location.
      /// </summary>
      /// <typeparam name="K">Key type.</typeparam>
      /// <typeparam name="V">Value type.</typeparam>
      /// <param name="dictionary">The dictionary to act on.</param>
      /// <param name="key1">The parent key.</param>
      /// <param name="key2">The child key.</param>
      /// <param name="value">The value to add or update.</param>
      public static void AddOrUpdate<K, V>(this Dictionary<K, Dictionary<K, V>> dictionary, K key1, K key2, V value)
      {
         Dictionary<K, V> foundDictionary;
         if (!dictionary.TryGetValue(key1, out foundDictionary))
         {
            foundDictionary = new Dictionary<K, V> { { key2, value } };
            dictionary.Add(key1, foundDictionary);
         }

         if (foundDictionary.ContainsKey(key2))
            foundDictionary[key2] = value;
         else
            foundDictionary.Add(key2, value);
      }
   }
}
