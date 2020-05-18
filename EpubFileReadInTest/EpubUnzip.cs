using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Windows.Storage;

namespace EpubFileReadInTest
{
    public static class EpubReadIn
    {

        public static async Task<string> EpubCopyAsync(StorageFile epubFile)
        {
            StorageFolder booksFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("books", CreationCollisionOption.OpenIfExists);
            StorageFile myFile = await epubFile.CopyAsync(booksFolder, epubFile.Name, NameCollisionOption.ReplaceExisting);
            return await UnZip(myFile, booksFolder);
        }

        public static async Task<string> UnZip(StorageFile copiedEpubFile, StorageFolder booksFolder)
        {
            string rootFolderString = booksFolder.Path + '\\' + copiedEpubFile.DisplayName;


            await Task.Run(async () =>
            {
                if (Directory.Exists(rootFolderString)) {
                    DirectoryInfo di = new DirectoryInfo(rootFolderString);
                    di.Delete(true);
                }
                StorageFolder rootFolder = await booksFolder.
                    CreateFolderAsync(copiedEpubFile.DisplayName, CreationCollisionOption.OpenIfExists);
                ZipFile.ExtractToDirectory(copiedEpubFile.Path, rootFolder.Path);
                await copiedEpubFile.DeleteAsync();

            });

            return rootFolderString;
        }

    }
    public static class EpubAnalyze
    {
        //Get the directory of content.opf by rootfolder ;
        public static async Task<string> GetContentPathAsync(String rootFolderString)
        {
            StorageFile container =
                    await StorageFile.GetFileFromPathAsync(rootFolderString + "\\META-INF\\container.xml");
            XDocument doc = XDocument.Load(new StreamReader(await container.OpenStreamForReadAsync()));
            XElement root = doc.Root;
            string fullPath = null;
            var rootfileList = root.Descendants(XName.Get("rootfile", "urn:oasis:names:tc:opendocument:xmlns:container"));
            foreach (var i in rootfileList) {
                fullPath = i.Attribute(XName.Get("full-path"))?.Value;
            }
            return fullPath.Replace('/', '\\');
        }
        public static async Task<string> GetContentDirectoryPathAsync(String rootFolderString)
        {
            return Path.GetDirectoryName(rootFolderString + '\\' +
                await GetContentPathAsync(rootFolderString));
        }
        public static async Task<Package> GetContentAsync(String rootFolderString)
        {
            string fullPath = (await GetContentPathAsync(rootFolderString));
            string contentPath = rootFolderString + '\\' + fullPath;

            //Declarations；
            StorageFile contentopf = await StorageFile.GetFileFromPathAsync(contentPath);

            string s;
            XmlSerializer xmlSearializer = new XmlSerializer(typeof(Package));
            Package package = new Package();

            //Read content.opf to s；
            using (Stream File = await contentopf.OpenStreamForReadAsync()) {
                using (StreamReader read = new StreamReader(File)) {
                    s = read.ReadToEnd();
                }
            }

            //Write properties to rootfile;
            using (StringReader sr = new StringReader(s)) {
                package = (Package)xmlSearializer.Deserialize(sr);
            }

            return package;
        }
        public static async Task<string> GetCoverPathAsync(Package myPackage, string rootFolderString)
        {
            Meta m = (Meta)myPackage.Metadata.Meta.Find(delegate (Meta p) { return p.Name == "cover"; });
            string returnStr;
            if (m != null) {
                string coverId = m.Content;
                Item i = new Item();
                i = (Item)myPackage.Manifest.Item.Find(delegate (Item p) { return p.Id == coverId; });

                returnStr = await GetContentDirectoryPathAsync(rootFolderString) + '\\' + i.Href.Replace('/', '\\');
            }
            else { returnStr = "not_found"; }
            return returnStr;
        }
        public static async Task<Bookinfo> GetBookinfo(String rootFolderString)
        {
            Package myPackage = new Package();
            myPackage = await GetContentAsync(rootFolderString);
            Bookinfo myBookInfo = new Bookinfo
            {
                Language = myPackage.Metadata.Language ?? "Unknown",
                Date = (myPackage.Metadata.Date != null) ? (myPackage.Metadata.Date.Text ?? "Unknown") : "Unknown",
                Creator = (myPackage.Metadata.Creator != null) ? (myPackage.Metadata.Creator.Text ?? "Unknown") : "Unknown",
                Publisher = myPackage.Metadata.Publisher ?? "Unknown",
                Title = myPackage.Metadata.Title ?? "Unknown"
            };
            string coverPath = await GetCoverPathAsync(myPackage, rootFolderString);
            if (coverPath != "not_found" && coverPath != null) {
                myBookInfo.Cover = await StorageFile.GetFileFromPathAsync(coverPath);
            }
            else {
                myBookInfo.Cover = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Square150x150Logo.scale-200.png"));
            }

            return myBookInfo;

        }
        public static async Task<StorageFile> GetNcxFile(Package myPackage, string rootFolderString)
        {
            Item i = (Item)myPackage.Manifest.Item.Find(delegate (Item p) { return p.Id == "ncx"; });
            string ncxPath = await GetContentDirectoryPathAsync(rootFolderString) + '\\' + i.Href.Replace('/', '\\');
            return await StorageFile.GetFileFromPathAsync(ncxPath);


        }
    }
    public static class EpubDelete
    {
        public static async Task DeleteBothAsync(String rootFolderString)
        {
            StorageFolder rootFolder = await StorageFolder.GetFolderFromPathAsync(rootFolderString);
            string copiedEpubFileString = rootFolderString + ".epub";
            StorageFile copiedEpubFile = await StorageFile.GetFileFromPathAsync(copiedEpubFileString);
            try {
                await rootFolder.DeleteAsync();
                await copiedEpubFile.DeleteAsync();
            }
            catch(Exception ex) { throw ex ; }
        }
        public static async Task DeleteDirAsync(string rootFolderString)
        {
            StorageFolder rootFolder = await StorageFolder.GetFolderFromPathAsync(rootFolderString);
            await rootFolder.DeleteAsync();
        }

    }


    public class Bookinfo
    {
        public StorageFile Cover { get; set; }
        public string Creator { get; set; }
        public string Publisher { get; set; }
        public string Date { get; set; }
        public string Language { get; set; }
        public string Title { get; set; }

    }

    /******Classes of content.opf******/
    //Parent of all book informations;
    [XmlRoot(ElementName = "package", Namespace = "http://www.idpf.org/2007/opf")]
    public class Package
    {
        [XmlElement(ElementName = "metadata", Namespace = "http://www.idpf.org/2007/opf")]
        public Metadata Metadata { get; set; }
        [XmlElement(ElementName = "manifest", Namespace = "http://www.idpf.org/2007/opf")]
        public Manifest Manifest { get; set; }
        [XmlElement(ElementName = "spine", Namespace = "http://www.idpf.org/2007/opf")]
        public Spine Spine { get; set; }
        [XmlElement(ElementName = "guide", Namespace = "http://www.idpf.org/2007/opf")]
        public Guide Guide { get; set; }
    }

    //get cover from here;
    [XmlRoot(ElementName = "meta", Namespace = "http://www.idpf.org/2007/opf")]
    public class Meta
    {
        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
        [XmlAttribute(AttributeName = "content")]
        public string Content { get; set; }
    }

    //creator.Text to access;
    [XmlRoot(ElementName = "creator", Namespace = "http://purl.org/dc/elements/1.1/")]
    public class Creator
    {
        [XmlText]
        public string Text { get; set; }
    }

    //date.Text to access;
    [XmlRoot(ElementName = "date", Namespace = "http://purl.org/dc/elements/1.1/")]
    public class Date
    {
        [XmlText]
        public string Text { get; set; }
    }

    //metadate the first branch(list of Meta & other meta data);
    [XmlRoot(ElementName = "metadata", Namespace = "http://www.idpf.org/2007/opf")]
    public class Metadata
    {
        [XmlElement(ElementName = "meta", Namespace = "http://www.idpf.org/2007/opf")]
        public List<Meta> Meta { get; set; }
        [XmlElement(ElementName = "creator", Namespace = "http://purl.org/dc/elements/1.1/")]
        public Creator Creator { get; set; }
        [XmlElement(ElementName = "date", Namespace = "http://purl.org/dc/elements/1.1/")]
        public Date Date { get; set; }
        [XmlElement(ElementName = "language", Namespace = "http://purl.org/dc/elements/1.1/")]
        public string Language { get; set; }
        [XmlElement(ElementName = "publisher", Namespace = "http://purl.org/dc/elements/1.1/")]
        public string Publisher { get; set; }
        [XmlElement(ElementName = "title", Namespace = "http://purl.org/dc/elements/1.1/")]
        public string Title { get; set; }
    }

    //sub of manifest;
    [XmlRoot(ElementName = "item", Namespace = "http://www.idpf.org/2007/opf")]
    public class Item
    {
        [XmlAttribute(AttributeName = "href")]
        public string Href { get; set; }
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }
        [XmlAttribute(AttributeName = "media-type")]
        public string Mediatype { get; set; }
    }

    //manifest the second branch(list of Item);
    [XmlRoot(ElementName = "manifest", Namespace = "http://www.idpf.org/2007/opf")]
    public class Manifest
    {
        [XmlElement(ElementName = "item", Namespace = "http://www.idpf.org/2007/opf")]
        public List<Item> Item { get; set; }
    }

    //sub of spine;
    [XmlRoot(ElementName = "itemref", Namespace = "http://www.idpf.org/2007/opf")]
    public class Itemref
    {
        [XmlAttribute(AttributeName = "idref")]
        public string Idref { get; set; }
    }

    //spine the third branch(list of Itemref);
    [XmlRoot(ElementName = "spine", Namespace = "http://www.idpf.org/2007/opf")]
    public class Spine
    {
        [XmlElement(ElementName = "itemref", Namespace = "http://www.idpf.org/2007/opf")]
        public List<Itemref> Itemref { get; set; }
    }

    //some page for the book's bigining;
    [XmlRoot(ElementName = "reference", Namespace = "http://www.idpf.org/2007/opf")]
    public class Reference
    {
        [XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }
        [XmlAttribute(AttributeName = "title")]
        public string Title { get; set; }
        [XmlAttribute(AttributeName = "href")]
        public string Href { get; set; }
    }

    //sorted list of table
    [XmlRoot(ElementName = "guide", Namespace = "http://www.idpf.org/2007/opf")]
    public class Guide
    {
        [XmlElement(ElementName = "reference", Namespace = "http://www.idpf.org/2007/opf")]
        public List<Reference> Reference { get; set; }
    }
    /******The Main Class is Package******/




}

namespace TableOfContent
{

    public class EpubTableAnalyze
    {
        public static async Task<List<NavPoint>> GetEpubTableAsync(StorageFile ncxFile)
        {
            List<NavPoint> NavPoints = new List<NavPoint>();
            string s;
            XmlSerializer xmlSearializer = new XmlSerializer(typeof(Ncx));
            Ncx myNcx = new Ncx();

            //Read toc.ncx to s；
            using (Stream File = await ncxFile.OpenStreamForReadAsync()) {
                using (StreamReader read = new StreamReader(File)) {
                    s = read.ReadToEnd();
                }
            }

            //Write properties to myNcx;
            using (StringReader sr = new StringReader(s)) {
                myNcx = (Ncx)xmlSearializer.Deserialize(sr);
            }
            NavPoints = myNcx.NavMap.NavPoints;
            return NavPoints;
        }
        public static List<NavPoint> GetSubPoints(NavPoint NavPoint)
        {
            List<NavPoint> subNavPoints = NavPoint.NavPoints;
            return subNavPoints;
        }
    }

    //Classes of toc.ncx;
    [XmlRoot(ElementName = "meta", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
    public class Meta
    {
        [XmlAttribute(AttributeName = "content")]
        public string Content { get; set; }
        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
    }

    [XmlRoot(ElementName = "head", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
    public class Head
    {
        [XmlElement(ElementName = "meta", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
        public List<Meta> Meta { get; set; }
    }

    [XmlRoot(ElementName = "docTitle", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
    public class DocTitle
    {
        [XmlElement(ElementName = "text", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "navLabel", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
    public class NavLabel
    {
        [XmlElement(ElementName = "text", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "content", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
    public class NavContent
    {
        [XmlAttribute(AttributeName = "src")]
        public string Src { get; set; }
    }

    [XmlRoot(ElementName = "navPoint", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
    public class NavPoint
    {
        [XmlElement(ElementName = "navLabel", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
        public NavLabel NavLabel { get; set; }
        [XmlElement(ElementName = "content", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
        public NavContent NavContent { get; set; }
        [XmlAttribute(AttributeName = "class")]
        public string Class { get; set; }
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }
        [XmlAttribute(AttributeName = "playOrder")]
        public string PlayOrder { get; set; }
        [XmlElement(ElementName = "navPoint", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
        public List<NavPoint> NavPoints { get; set; }
    }

    [XmlRoot(ElementName = "navMap", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
    public class NavMap
    {
        [XmlElement(ElementName = "navPoint", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
        public List<NavPoint> NavPoints { get; set; }
    }

    [XmlRoot(ElementName = "ncx", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
    public class Ncx
    {
        [XmlElement(ElementName = "head", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
        public Head Head { get; set; }
        [XmlElement(ElementName = "docTitle", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
        public DocTitle DocTitle { get; set; }
        [XmlElement(ElementName = "navMap", Namespace = "http://www.daisy.org/z3986/2005/ncx/")]
        public NavMap NavMap { get; set; }
        [XmlAttribute(AttributeName = "xmlns")]
        public string Xmlns { get; set; }
        [XmlAttribute(AttributeName = "version")]
        public string Version { get; set; }
        [XmlAttribute(AttributeName = "lang", Namespace = "http://www.w3.org/XML/1998/namespace")]
        public string Lang { get; set; }
    }
}
