// Learn more about F# at http://fsharp.org

open System
open System.IO
open System.Net
open System.Text
open System.Web

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Configuration.Json


[<EntryPoint>]
let main argv =
    
    let builder = ConfigurationBuilder().AddJsonFile("appsettings.json")
    let config = builder.Build()

    let clientId = config.["clientId"] 
    let userName = config.["userName"]  |> HttpUtility.UrlEncode   
    let password = config.["password"]  |> HttpUtility.UrlEncode   
    let tenantId = config.["tenantId"]
    let groupId = config.["groupId"]
    let reportId = config.["reportId"]

    let authTokenUrl = sprintf "https://login.windows.net/%s/oauth2/token" tenantId
    let powerBITokeUrl = sprintf "https://api.powerbi.com/v1.0/myorg/groups/%s/reports/%s/GenerateToken" groupId reportId
   
    let adAuthData = 
        [ "resource",("https://analysis.windows.net/powerbi/api" |> HttpUtility.UrlEncode ) ; 
          "client_id",clientId;
          "grant_Type","password";
          "userName",userName;
          "password",password;
          "scope","openid" ] 
          |> List.fold(fun s (n,v) -> sprintf "%s=%s&%s" n v s) ""
          |> Encoding.ASCII.GetBytes
    let adAuthRequest = WebRequest.CreateHttp(authTokenUrl)
    adAuthRequest.Method <- "POST"
    adAuthRequest.ContentType <- "application/x-www-form-urlencoded"
    adAuthRequest.ContentLength <- adAuthData.Length |> int64

    use adAuthPostStream = adAuthRequest.GetRequestStream()
    adAuthPostStream.Write(adAuthData,0,adAuthData.Length)

    let adAuthResponse = adAuthRequest.GetResponse()

    let adAuthResponseObj = 
        (new StreamReader( adAuthResponse.GetResponseStream())).ReadToEnd()
        |> JObject.Parse

    
    let accessToken = adAuthResponseObj.GetValue("access_token").ToString()

   
    
    let powerBIRequestBody =  
        JObject(JProperty("accessLevel","view")).ToString(Formatting.Indented) 
        |> Encoding.ASCII.GetBytes
    let powerBIRequest = WebRequest.CreateHttp(powerBITokeUrl)
    powerBIRequest.Method <- "POST"
    powerBIRequest.ContentType <- "application/json"
    powerBIRequest.ContentLength <- powerBIRequestBody.Length |> int64
    powerBIRequest.Headers.Add("Authorization",(sprintf "Bearer %s" accessToken))

    use powerBIPostStream = powerBIRequest.GetRequestStream()
    powerBIPostStream.Write(powerBIRequestBody,0,powerBIRequestBody.Length)

    use powerBIResponse = powerBIRequest.GetResponse()

    let powerBIResponseObj = 
        (new StreamReader( powerBIResponse.GetResponseStream())).ReadToEnd() 
        |> JObject.Parse
    
   
    let embedToken = powerBIResponseObj.GetValue("token").ToString()
    let embedUrl = sprintf "https://app.powerbi.com/reportEmbed?reportId=%s&groupId=%s" reportId groupId

    printfn "Embed Token:\n\t%s\n" embedToken
    printfn "Embed URL:\n\t%s\n" embedUrl
    printfn "Report Id:\n\t%s\n" reportId
    0 