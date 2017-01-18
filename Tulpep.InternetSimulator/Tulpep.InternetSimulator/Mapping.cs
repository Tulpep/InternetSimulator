using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tulpep.InternetSimulator
{
    public class Mapping
    {
        public string OriginalEntry { get; set; }
        public bool ParsingSuccess { get; set; }
        public string ParsingMessage { get; set; }
        public string Uri { get; set; }
        public string UriScheme { get; set; }
        public string Domain { get; set; }
        public string FilePath { get; set; }
        public FileBehavior Behavior { get; set; }

    }

    public enum FileBehavior
    {
        Web, File
    }
}
