using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ImageResizer;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using CognitiveServices.Models;
using System.Threading.Tasks;
using System.IO;
using Microsoft.ProjectOxford.Vision;

namespace CognitiveServices.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Home()
        {
            return View();
        }
        [OutputCache(Duration = 35, VaryByParam = "none")] // Cache Response set to last for 30 seconds  
        public ActionResult Index(string id)
        {
            // Pass a list of blob URIs and captions in ViewBag
            CloudStorageAccount account = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference("photos");
            List<StorageBlobs> blobs = new List<StorageBlobs>();

            foreach (IListBlobItem item in container.ListBlobs())
            {
                var blob = item as CloudBlockBlob;

                if (blob != null)
                {
                    blob.FetchAttributes(); // Get blob metadata

                    if (String.IsNullOrEmpty(id) || HasMatchingMetadata(blob, id))
                    {
                        var caption = blob.Metadata.ContainsKey("Caption") ? blob.Metadata["Caption"] : blob.Name;
                        var category= blob.Metadata.ContainsKey("Category") ? blob.Metadata["Category"] : blob.Name;
                        var imagetype= blob.Metadata.ContainsKey("ImageType") ? blob.Metadata["ImageType"] : blob.Name;
                        var color= blob.Metadata.ContainsKey("Color") ? blob.Metadata["Color"] : blob.Name;

                        
                        blobs.Add(new StorageBlobs()
                        {
                            ImageUri = blob.Uri.ToString(),
                            ThumbnailUri = blob.Uri.ToString().Replace("/photos/", "/thumbnails/"),
                            Caption = caption,
                            ImageType = imagetype,
                            Color=color,
                            Category=category
                        });
                    }
                }
            }

            ViewBag.Blobs = blobs.ToArray();
            ViewBag.Search = id; // Prevent search box from losing its content
            return View();
        }
        private bool HasMatchingMetadata(CloudBlockBlob blob, string term)
        {
            foreach (var item in blob.Metadata)
            {
                if (item.Key.StartsWith("Tag") && item.Value.Equals(term, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            return false;
        }
        [HttpPost]
        public ActionResult Search(string term)
        {
            return RedirectToAction("Index", new { id = term });
        }
        [HttpPost]
        public async Task<ActionResult> Upload(HttpPostedFileBase file)
        {
            if (file != null && file.ContentLength > 0)
            {
                // Make sure the user selected an image file
                if (!file.ContentType.StartsWith("image"))
                {
                    TempData["Message"] = "Only image files may be uploaded";
                }
                else
                {
                    // Save the original image in the "photos" container
                    CloudStorageAccount account = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
                    CloudBlobClient client = account.CreateCloudBlobClient();
                    CloudBlobContainer container = client.GetContainerReference("photos");
                    CloudBlockBlob photo = container.GetBlockBlobReference(Path.GetFileName(file.FileName));
                    await photo.UploadFromStreamAsync(file.InputStream);
                    file.InputStream.Seek(0L, SeekOrigin.Begin);

                    // Generate a thumbnail and save it in the "thumbnails" container

                    using (var outputStream = new MemoryStream())
                    {
                        var settings = new ResizeSettings { MaxWidth = 192, Format = "png" };
                        ImageBuilder.Current.Build(file.InputStream, outputStream, settings);
                        outputStream.Seek(0L, SeekOrigin.Begin);
                        container = client.GetContainerReference("thumbnails");
                        CloudBlockBlob thumbnail = container.GetBlockBlobReference(Path.GetFileName(file.FileName));
                        await thumbnail.UploadFromStreamAsync(outputStream);
                    }
                    // Submit the image to Azure's Computer Vision API
                    VisionServiceClient vision = new VisionServiceClient(
                        CloudConfigurationManager.GetSetting("SubscriptionKey"),
                        CloudConfigurationManager.GetSetting("VisionEndpoint")
                    );

                    VisualFeature[] features = new VisualFeature[] { VisualFeature.Description };
                    var result = await vision.AnalyzeImageAsync(photo.Uri.ToString(), features);

                    // Record the image description and tags in blob metadata
                    photo.Metadata.Add("Caption", result.Description.Captions[0].Text);

                    for (int i = 0; i < result.Description.Tags.Length; i++)
                    {
                        string key = String.Format("Tag{0}", i);
                        photo.Metadata.Add(key, result.Description.Tags[i]);
                    }

                    await photo.SetMetadataAsync();
                }
            }

            // redirect back to the index action to show the form once again
            return RedirectToAction("Index");
        }


            public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}