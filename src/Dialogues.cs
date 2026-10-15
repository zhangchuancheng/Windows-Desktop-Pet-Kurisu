using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace MakiseDesktopPet
{
    internal sealed class Dialogues
    {
        private readonly Dictionary<string, string[]> groups;
        private readonly Random random = new Random();
        private string previous;

        internal Dialogues(string filename)
        {
            if (!File.Exists(filename))
                throw new FileNotFoundException("Missing dialogue file: " + filename, filename);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            groups = serializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(filename, Encoding.UTF8));
            foreach (string category in new[] { "click", "rapid", "wake", "drag" })
                if (groups == null || !groups.ContainsKey(category) || groups[category].Length < 2)
                    throw new InvalidDataException("dialogues.json needs at least two lines in " + category + ".");
        }

        internal string Pick(string category)
        {
            string[] lines = groups[category];
            List<string> choices = new List<string>();
            foreach (string line in lines)
                if (!String.IsNullOrWhiteSpace(line) && line != previous)
                    choices.Add(line);
            if (choices.Count == 0)
                throw new InvalidDataException("No usable dialogue in " + category + ".");
            previous = choices[random.Next(choices.Count)];
            return previous;
        }
    }
}
