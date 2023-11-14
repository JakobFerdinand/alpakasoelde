module Shared exposing (Data, Model, Msg(..), SharedMsg(..), template)

import BackendTask exposing (BackendTask)
import Effect exposing (Effect)
import Element exposing (..)
import Element.Font as Font
import Element.Region as Region
import FatalError exposing (FatalError)
import Html exposing (Html)
import Pages.Flags
import Pages.PageUrl exposing (PageUrl)
import Route exposing (Route)
import SharedTemplate exposing (SharedTemplate)
import UrlPath exposing (UrlPath)
import View exposing (View)


template : SharedTemplate Msg Model Data msg
template =
    { init = init
    , update = update
    , view = view
    , data = data
    , subscriptions = subscriptions
    , onPageChange = Nothing
    }


type Msg
    = SharedMsg SharedMsg


type alias Data =
    ()


type SharedMsg
    = NoOp


type alias Model =
    {}


init :
    Pages.Flags.Flags
    ->
        Maybe
            { path :
                { path : UrlPath
                , query : Maybe String
                , fragment : Maybe String
                }
            , metadata : route
            , pageUrl : Maybe PageUrl
            }
    -> ( Model, Effect Msg )
init flags maybePagePath =
    ( {}
    , Effect.none
    )


update : Msg -> Model -> ( Model, Effect Msg )
update msg model =
    case msg of
        SharedMsg globalMsg ->
            ( model, Effect.none )


subscriptions : UrlPath -> Model -> Sub Msg
subscriptions _ _ =
    Sub.none


data : BackendTask FatalError Data
data =
    BackendTask.succeed ()


view :
    Data
    ->
        { path : UrlPath
        , route : Maybe Route
        }
    -> Model
    -> (Msg -> msg)
    -> View msg
    -> { body : List (Html msg), title : String }
view sharedData page model toMsg pageView =
    { body =
        [ layout [ width fill, height fill, padding 10 ] <|
            column [ width fill, height fill ]
                [ header
                , content pageView.body
                ]
        ]
    , title = pageView.title
    }


header : Element msg
header =
    row
        [ Region.navigation
        , width fill
        , alignTop
        , padding 16
        , spacing 16
        ]
        [ link [ alignLeft ] { url = "/", label = el [ Font.bold, Font.size 36 ] <| text "🦙" }
        , link [ alignRight ] { url = Route.toString (Route.Blog__Slug_ { slug = "hello" }), label = text "Blog" }
        , link [ alignRight ] { url = Route.toString Route.Greet, label = text "Greet" }
        , link [ alignRight ] { url = Route.toString Route.Hello, label = text "Hello" }
        ]


content : List (Element msg) -> Element msg
content body =
    column [ Region.mainContent, width fill, height fill ] body
