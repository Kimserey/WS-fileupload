namespace FormSubmitMime

open WebSharper
open WebSharper.UI.Next
open WebSharper.UI.Next.Html
open WebSharper.Sitelets

type EndPoint =
    | [<EndPoint "GET /">] Home
    | [<EndPoint "POST /upload">] Upload

module Site =
    open WebSharper.UI.Next.Server

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
                                  attr.action "/upload" ]
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
