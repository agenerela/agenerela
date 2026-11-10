using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Agenerela
{
    /// <summary>
    /// Reads a file a <see cref="DecisionLog"/> wrote back into <see cref="DecisionLogLine"/>s, in
    /// file order (#190, G7). The Decision Log window (#172), a replay and the tests read through
    /// this, so the format has one definition.
    /// </summary>
    /// <remarks>
    /// A line that is blank is passed over. A line that cannot be read, such as the partial last
    /// line of a game that died mid-write, is counted in <c>skipped</c> and passed over, never
    /// thrown. A line from a later version is read for the fields this version knows. To read a log
    /// that rolled over, read <see cref="DecisionLog.RolloverPath"/> first: it holds the older lines.
    /// </remarks>
    public static class DecisionLogReader
    {
        /// <summary>Every line of the file at <paramref name="path"/>, oldest first.</summary>
        /// <exception cref="IOException">The file cannot be opened.</exception>
        public static List<DecisionLogLine> Read(string path)
        {
            return Read(path, out _);
        }

        /// <summary>
        /// Every line of the file at <paramref name="path"/>, oldest first, and how many lines could
        /// not be read. The file may be open in a running log meanwhile.
        /// </summary>
        /// <exception cref="IOException">The file cannot be opened.</exception>
        public static List<DecisionLogLine> Read(string path, out int skipped)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
            {
                var lines = new List<string>();
                string text;
                while ((text = reader.ReadLine()) != null)
                {
                    lines.Add(text);
                }

                return Parse(lines, out skipped);
            }
        }

        /// <summary>Reads lines already in memory, such as a pasted export, the same way.</summary>
        public static List<DecisionLogLine> Parse(IEnumerable<string> lines, out int skipped)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            var read = new List<DecisionLogLine>();
            skipped = 0;
            foreach (var text in lines)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (TryParse(text, out var line))
                {
                    read.Add(line);
                }
                else
                {
                    skipped++;
                }
            }

            return read;
        }

        /// <summary>One line of a log. False, with <paramref name="line"/> null, when it cannot be read.</summary>
        public static bool TryParse(string text, out DecisionLogLine line)
        {
            line = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                line = JsonConvert.DeserializeObject<DecisionLogLine>(text, DecisionLogFormat.Settings);
                if (line != null && line.Version >= 1)
                {
                    return true;
                }

                line = null;
                return false;
            }
            catch (Exception e) when (e is JsonException || e is ArgumentException || e is FormatException || e is InvalidCastException)
            {
                line = null;
                return false;
            }
        }
    }
}
