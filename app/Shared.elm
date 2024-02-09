module Shared exposing (Data, Model, Msg(..), SharedMsg(..), template)

import BackendTask exposing (BackendTask)
import Browser.Events
import Effect exposing (Effect)
import Element exposing (..)
import Element.Font as Font
import Element.Region as Region
import FatalError exposing (FatalError)
import Html exposing (Html)
import Json.Decode as Decode
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
    | WindowSizeChanged Device


type alias Data =
    ()


type SharedMsg
    = NoOp


type alias Model =
    { device : Device }


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
    let
        classify : Int -> Int -> Device
        classify height width =
            classifyDevice { height = height, width = width }

        decodeFlags =
            Decode.decodeValue
                (Decode.map2 classify
                    (Decode.field "height" Decode.int)
                    (Decode.field "width" Decode.int)
                )

        getDevice : Device
        getDevice =
            case flags of
                Pages.Flags.PreRenderFlags ->
                    { class = Desktop
                    , orientation = Portrait
                    }

                Pages.Flags.BrowserFlags value ->
                    case decodeFlags value of
                        Ok device ->
                            device

                        Err _ ->
                            { class = Desktop
                            , orientation = Portrait
                            }
    in
    ( { device = getDevice }
    , Effect.none
    )


update : Msg -> Model -> ( Model, Effect Msg )
update msg model =
    case msg of
        SharedMsg globalMsg ->
            ( model, Effect.none )

        WindowSizeChanged device ->
            ( { model | device = device }, Effect.none )


subscriptions : UrlPath -> Model -> Sub Msg
subscriptions _ _ =
    Browser.Events.onResize
        (\w h -> WindowSizeChanged <| classifyDevice { height = h, width = w })


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
        ]


content : List (Element msg) -> Element msg
content body =
    column [ Region.mainContent, width fill, height fill ] body
