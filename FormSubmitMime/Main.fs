namespace FormSubmitMime

open System
open System.IO
open WebSharper
open WebSharper.UI.Next
open WebSharper.UI.Next.Html
open WebSharper.Sitelets

type EndPoint =
    | [<EndPoint "GET /">] Home
    | [<EndPoint "POST /upload">] Upload

module Site =
    open WebSharper.UI.Next.Server
    open MimeKit
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
                                    [ attr.name "Title"
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
                let load (c: ContentType) (b: Stream) = MimeEntity.Load(c, b)
                
                match ctx.Request.Headers |> Seq.tryFind (fun (header: Header) -> String.Equals(header.Name, "content-type", StringComparison.OrdinalIgnoreCase)) with
                | Some header ->
                    let contentType = ContentType.Parse (header.Value)
                    match load contentType ctx.Request.Body with
                    | :? MimeKit.Multipart as multipart -> 
                        multipart
                        |> Seq.map(fun part ->
                            match part with
                            | :? MimeKit.TextPart as m -> 
                                m.ContentDisposition.Parameters.Item "name" + ": " + m.Text
                            | :? MimeKit.MimePart as m -> 
                                Directory.CreateDirectory "data" |> ignore
                                use file = File.OpenWrite(Path.Combine("data", m.FileName))
                                m.ContentObject.Stream.CopyTo(file)
                                m.ContentDisposition.Parameters.Item "name" + ": " + m.FileName
                            | _ -> "")
                        |> String.concat ","
                        |> Content.Text
                    | _ ->
                        Content.Text "test"
                | _ ->
                    Content.Text "test"
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
