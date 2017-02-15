namespace FormSubmitMime

open System
open System.IO
open WebSharper
open WebSharper.UI.Next
open WebSharper.UI.Next.Html
open WebSharper.Sitelets

module MultipartFormData =
    open MimeKit
    
    type MultipartFormDataResult =
        | Values of MimePartResult list
        | MimePartNotSupported
        
    and MimePartResult =
        | Text of fieldname:string * value:string
        | File of fieldname:string * filename:string * Stream

    let decode (contentType: string) (body: Stream) =
        let load (c: ContentType) (b: Stream) = MimeEntity.Load(c, b)
        let contentType = ContentType.Parse contentType

        match load contentType body with
        | :? MimeKit.Multipart as multipart -> 
            multipart
            |> Seq.choose(fun part ->
                match part with
                | :? MimeKit.TextPart as m -> Some <| MimePartResult.Text (m.ContentDisposition.Parameters.Item "name", m.Text)
                | :? MimeKit.MimePart as m -> Some <| MimePartResult.File (m.ContentDisposition.Parameters.Item "name", m.FileName, m.ContentObject.Stream)
                | _ -> None)
            |> Seq.toList
            |> Values

        | _ -> MimePartNotSupported

type EndPoint =
    | [<EndPoint "GET /">] Home
    | [<EndPoint "POST /upload">] Upload

module Site =
    open WebSharper.UI.Next.Server
    open WebSharper.Web
    open WebSharper.Sitelets.Http

    type MainTemplate = Templating.Template<"Main.html">

    [<Website>]
    let Main =

        Application.MultiPage (fun ctx action ->
            match action with
            | Home -> 
                Content.Page(
                    MainTemplate.Doc(
                        "Home", 
                        [
                            formAttr
                                [ attr.``method`` "post"
                                  attr.action "/upload"
                                  attr.enctype "multipart/form-data" ]
                                [ inputAttr
                                    [ attr.id "Title"
                                      attr.``type`` "text" ]
                                    []
                                  inputAttr
                                    [ attr.name "File"
                                      attr.multiple ""
                                      attr.``type`` "file" ]
                                    []
                                  buttonAttr [ attr.``type`` "submit" ] [ text "Submit" ] ]
                    
                        ]
                    )
                )
            | Upload ->
                match ctx.Request.Headers |> Seq.tryFind (fun (header: Header) -> String.Equals(header.Name, "content-type", StringComparison.OrdinalIgnoreCase)) with
                | Some header ->
                    match MultipartFormData.decode header.Value ctx.Request.Body with
                    | MultipartFormData.Values values ->
                        values
                        |> List.map (
                            function
                            | MultipartFormData.Text (name, value) -> name + " " + value
                            | MultipartFormData.File (name, filename, stream) -> 
                                use file = File.OpenWrite(Path.Combine("data", filename))
                                stream.CopyTo(file)
                                name + " " + filename
                        )
                        |> String.concat ", "
                        |> Content.Text

                    | MultipartFormData.MimePartNotSupported ->
                        Content.MethodNotAllowed
                | _ -> Content.NotFound
        )


module SelfHostedServer =

    open global.Owin
    open Microsoft.Owin.Hosting
    open Microsoft.Owin.StaticFiles
    open Microsoft.Owin.FileSystems
    open WebSharper.Owin

    [<EntryPoint>]
    let Main args =
        let rootDirectory, url =
            match args with
            | [| rootDirectory; url |] -> rootDirectory, url
            | [| url |] -> "..", url
            | [| |] -> "..", "http://localhost:9000/"
            | _ -> eprintfn "Usage: FormSubmitMime ROOT_DIRECTORY URL"; exit 1
        use server = WebApp.Start(url, fun appB ->
            appB.UseStaticFiles(
                    StaticFileOptions(
                        FileSystem = PhysicalFileSystem(rootDirectory)))
                .UseSitelet(rootDirectory, Site.Main)
            |> ignore)
        stdout.WriteLine("Serving {0}", url)
        stdin.ReadLine() |> ignore
        0
