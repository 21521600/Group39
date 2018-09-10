using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CognitiveServices.Models
{
    public class StorageBlobs
    {
        public string ImageUri { get; set; }
        public string ThumbnailUri { get; set; }
        public string Caption { get; set; }
        public string Category { get; set; }
        public string ImageType { get; set; }
        public string Color { get; set; }
    }
}