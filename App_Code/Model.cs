using System;
using System.IO;
using System.Web;
using System.Web.Helpers;
using System.Linq;
using WebMatrix.Data;
using System.Web.WebPages.Html;

/// <summary>
/// Database model class
/// </summary>
public class Model {
    Database db;
    
    public Model() {
        db = Database.Open(HttpContext.Current.Application["Database"].ToString());
    }
    
    public dynamic Search(string Query) {
        return db.Query("SELECT p.*, Nickname FROM Posts p JOIN UserProfile u ON p.UserId = u.UserId WHERE Title LIKE @0 OR Content LIKE @0", "%"+Query+"%");
    }
    
    public dynamic GetArchive() {
        return db.Query("SELECT id, Title, Published, Nickname [Author], Slug FROM Posts p JOIN UserProfile u ON p.UserId = u.UserId ORDER BY Published DESC");
    }
    
    public dynamic GetLatest(int Count) {
        return db.Query("SELECT p.*, Nickname FROM Posts p JOIN UserProfile u ON p.UserId = u.UserId ORDER BY Published DESC").Take(Count).ToList();
    }
    
    public dynamic GetPost(int PostId) {
        return db.QuerySingle("SELECT p.*, Nickname FROM Posts p JOIN UserProfile u ON p.UserId = u.UserId WHERE id = @0", PostId);
    }
    
    public dynamic GetPost(int PostId, string Slug) {
        return db.QuerySingle("SELECT p.*, Nickname FROM Posts p JOIN UserProfile u ON p.UserId = u.UserId WHERE id = @0 AND LOWER(Slug) = @1", PostId, Slug.ToLower());
    }
    
    public dynamic NewPost(int UserId) {
        return db.QuerySingle("SELECT 0 [id], " + UserId + " [UserId], GetDate() [Published], null [Modified], '' [Title], '' [Content], '' [Slug]");
    }
    
    public int SavePost(HttpRequestBase Request, ModelStateDictionary ms) {
        int postId, userId;
        string Title = Request.Unvalidated("Title"),
               Content = Request.Unvalidated("Content");
        
        int.TryParse(Request.Unvalidated().Form["id"], out postId);
        int.TryParse(Request.Unvalidated().Form["UserId"], out userId);        
        
        if(string.IsNullOrWhiteSpace(Title))
            ms.AddError("Title", "Title cannot be empty!");
        if(Title.Length > 100)
            ms.AddError("Title", "Title is too long!");
        if(string.IsNullOrWhiteSpace(Content))
            ms.AddError("Content", "You have to post something!");
                
        if(!ms.IsValid)
            return 0;                        
        
        // tiny_mce bug?
        Content = Content.Replace("<p>&nbsp;</p>", "");
        
        if(postId <= 0) { 
            // Insert
            string Slug = string.Empty;
            foreach(var c in Title)
                if(char.IsLetterOrDigit(c))
                    Slug += c;
                else if(char.IsWhiteSpace(c))
                    Slug += "-";
            
            db.Execute("INSERT INTO Posts (UserId, Published, Title, Content, Slug) VALUES (@0, @1, @2, @3, @4)", userId, DateTime.Now, Title, Content, Slug);
            postId = (int)db.GetLastInsertId();
        } else {          
            // Update
            db.Execute("UPDATE Posts SET Title=@0, Content=@1, Modified=@2 WHERE id=@3", Title, Content, DateTime.Now, postId);
        }
        
        return postId;
    }
    
    public bool DeletePost(int PostId) {
        return db.Execute("DELETE FROM Posts WHERE id=@0", PostId) > 0;
    }
    
    public dynamic GetGalleries() {
        return db.Query("SELECT Galleries.Id, Galleries.Name, COUNT(Images.Id) AS ImageCount " +
                        "FROM Galleries LEFT OUTER JOIN Images ON Galleries.Id = Images.GalleryId " +
                        "GROUP BY Galleries.Id, Galleries.Name");
    }
    
    public dynamic GetGallery(int GalleryId) {
        return db.QuerySingle("SELECT * FROM Galleries WHERE id = @0", GalleryId);
    }
    
    public dynamic GetGalleryImages(int GalleryId) {
        return db.Query("SELECT * FROM Images WHERE GalleryId = @0 ORDER BY UploadDate DESC", GalleryId);
    }
    
    public dynamic GetLatestInGallery(int GalleryId, int Count) {
        return db.Query("SELECT TOP(" + Count + ") Contents, UploadDate FROM Images WHERE GalleryId = @0 ORDER BY UploadDate DESC", GalleryId);
    }
    
    public bool CreateGallery(string Name) {
        return db.Execute("INSERT INTO Galleries (Name) VALUES (@0)", Name) > 0;
    }
    
    public dynamic GetImage(int ImageId) {
        return db.QuerySingle("SELECT * FROM Images WHERE id = @0", ImageId);
    }    
    
    public dynamic GetImageThumbnail(int Width, int Height, int ImageId) {
        var img = db.QuerySingle("SELECT Contents FROM Images WHERE id = @0", ImageId);
        return new WebImage(img.Contents).Resize(Width, Height);
    }
    
    public bool UploadImage(int GalleryId, int UserId, HttpPostedFileBase file) {
        var wi = new WebImage(file.InputStream);
        var name = Path.GetFileNameWithoutExtension(file.FileName).Trim();
        var ext = Path.GetExtension(file.FileName).Trim();
        var contents = wi.GetBytes();
        
        if(string.IsNullOrWhiteSpace(name)) {
            name = "Untitled";
        }
                
        return db.Execute("INSERT INTO Images (GalleryId, UserId, Name, Extension, ContentType, Size, UploadDate, Contents, Width, Height) VALUES " +
                          "(@0, @1, @2, @3, @4, @5, @6, @7, @8, @9)",
                          GalleryId, UserId, name, ext, wi.ImageFormat, contents.Length, DateTime.Now, contents, wi.Width, wi.Height) > 0;
    }
    
    public bool DeleteGallery(int GalleryId) {
        db.Execute("DELETE FROM Images WHERE GalleryId = @0", GalleryId);
        return db.Execute("DELETE FROM Galleries WHERE id = @0", GalleryId) > 0;
    }
    
    public bool DeleteImage(int ImageId) {
        return db.Execute("DELETE FROM Images WHERE id = @0", ImageId) > 0;
    }
}