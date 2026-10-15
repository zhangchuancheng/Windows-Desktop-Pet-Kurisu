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
        private readonly Random random;
        private readonly Dictionary<string, List<string>> bags = new Dictionary<string, List<string>>();
        private string previous;

        internal static readonly string[] Categories =
            { "click-block", "click-fold", "click-cheek", "rapid", "wake", "drag", "thought", "company" };

        internal Dialogues(string filename) : this(filename, new Random()) { }

        internal Dialogues(string filename, Random random)
        {
            this.random = random;
            if (!File.Exists(filename))
                throw new FileNotFoundException("Missing dialogue file: " + filename, filename);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            groups = serializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(filename, Encoding.UTF8));
            foreach (string category in Categories)
            {
                if (groups == null || !groups.ContainsKey(category) || groups[category] == null)
                    throw new InvalidDataException("Missing dialogue category: " + category + ".");
                List<string> usable = new List<string>();
                foreach (string line in groups[category])
                    if (!String.IsNullOrWhiteSpace(line) && !usable.Contains(line.Trim())) usable.Add(line.Trim());
                if (usable.Count < 2)
                    throw new InvalidDataException("dialogues.json needs at least two distinct non-empty lines in " + category + ".");
                groups[category] = usable.ToArray();
                bags[category] = new List<string>();
            }
        }

        internal string PickClick(Pose pose)
        {
            string specific;
            switch (pose)
            {
                case Pose.PokeBlock: specific = "click-block"; break;
                case Pose.PokeFold: specific = "click-fold"; break;
                case Pose.PokeCheek: specific = "click-cheek"; break;
                default: return Pick("click");
            }
            // Reflective lines suit the first two poses; softer lines suit the cheek pose.
            return Pick(random.Next(4) == 0
                ? (pose == Pose.PokeCheek ? "company" : "thought") : specific);
        }

        internal string Pick(string category)
        {
            if (category == "click") category = random.Next(2) == 0 ? "thought" : "company";
            List<string> bag = bags[category];
            if (bag.Count == 0)
            {
                bag.AddRange(groups[category]);
                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    string swap = bag[i]; bag[i] = bag[j]; bag[j] = swap;
                }
            }
            int index = bag.Count - 1;
            if (bag[index] == previous)
            {
                // Defer a shared custom line if another group just used it.
                if (index == 0)
                {
                    List<string> alternatives = new List<string>();
                    foreach (string line in groups[category]) if (line != previous) alternatives.Add(line);
                    previous = alternatives[random.Next(alternatives.Count)];
                    return previous;
                }
                index = random.Next(index);
            }
            previous = bag[index];
            bag.RemoveAt(index);
            return previous;
        }
    }
}
