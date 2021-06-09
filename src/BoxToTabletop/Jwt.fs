namespace BoxToTabletop

module Jwt =

  open System.Text
  open BoxToTabletop.Configuration
  open Microsoft.IdentityModel.Tokens
  open Microsoft.AspNetCore.Authentication.JwtBearer
  open System.IdentityModel.Tokens.Jwt

  let createIssuer config =  $"https://{config.Auth0Config.Domain}/"

  let createValidationParams config =
    let issuer = createIssuer config
    let validationParams =  new TokenValidationParameters()
    validationParams.ValidateIssuer <- true
    validationParams.ValidateIssuerSigningKey <- true
    validationParams.ValidateAudience <- true
    validationParams.ValidAudience <- config.Auth0Config.Audience
    validationParams.ValidIssuer <- issuer
    validationParams.IssuerSigningKey <- new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Auth0Config.ClientId))
    validationParams.ValidAlgorithms <- [| "RS256" |]
    validationParams

  let readToken token =
    let handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
    handler.ReadJwtToken(token)

  let printAllClaims (token : JwtSecurityToken) =
    for claim in token.Claims do
      printfn "Claim: %A" claim

  let getUserId (token : System.IdentityModel.Tokens.Jwt.JwtSecurityToken) =
    printAllClaims token
    token.Claims |> Seq.tryFind (fun claim -> claim.Type = "sub") |> Option.map (fun claim -> claim.Value)