module Route.Index exposing (ActionData, Data, Model, Msg, route)

import BackendTask exposing (BackendTask)
import Css exposing (maxHeight)
import Element exposing (..)
import Element.Font as Font
import Element.Region as Region
import FatalError exposing (FatalError)
import Head
import Head.Seo as Seo
import Html.Attributes as HtmlAttributes
import Pages.Url
import PagesMsg exposing (PagesMsg)
import Route
import RouteBuilder exposing (App, StatelessRoute)
import Shared
import Svg as Svg exposing (svg)
import Svg.Attributes as SvgAttributes
import UrlPath
import View exposing (View)


type alias Model =
    {}


type alias Msg =
    ()


type alias RouteParams =
    {}


type alias Data =
    { message : String
    }


type alias ActionData =
    {}


route : StatelessRoute RouteParams Data ActionData
route =
    RouteBuilder.single
        { head = head
        , data = data
        }
        |> RouteBuilder.buildNoState { view = view }


data : BackendTask FatalError Data
data =
    BackendTask.succeed Data
        |> BackendTask.andMap
            (BackendTask.succeed "Hello!")


head :
    App Data ActionData RouteParams
    -> List Head.Tag
head app =
    Seo.summary
        { canonicalUrlOverride = Nothing
        , siteName = "Alpakasölde"
        , image =
            { url = [ "images", "icon-png.png" ] |> UrlPath.join |> Pages.Url.fromPath
            , alt = "Alpakasölde Logo"
            , dimensions = Nothing
            , mimeType = Nothing
            }
        , description = "Schön, dass sie die Seite der Alpakasölde besuchen."
        , locale = Nothing
        , title = "Alpakasölde"
        }
        |> Seo.website


view :
    App Data ActionData RouteParams
    -> Shared.Model
    -> View (PagesMsg Msg)
view app shared =
    let
        contentPadding =
            case shared.device.class of
                Phone ->
                    padding 20

                Tablet ->
                    paddingXY 80 10

                Desktop ->
                    paddingXY 120 10

                BigDesktop ->
                    paddingXY 180 10
    in
    { title = "Alpakasölde"
    , body =
        [ column [ contentPadding, centerY, width fill ]
            [ image [ width (fill |> maximum 512), centerX, height (fill |> maximum 512) ] { src = "alpakafarm.svg", description = "Alpakasölde" }
            , column [ centerX, Font.center, spacing 30 ]
                [ el [ centerX, Region.heading 1, Font.size 52 ] (text "ALPAKASÖLDE")
                , column [ centerX, spacing 10 ]
                    [ paragraph [] [ text "Willkommen auf unserer Alpaka-Farm!" ]
                    , paragraph [] [ text "Wir freuen uns, dass Sie den Weg zu uns gefunden haben und möchten Sie in die zauberhafte Welt unserer Alpakas entführen. Unsere Farm ist ein Ort der Ruhe und des Staunens, wo Sie die Gelegenheit haben, die faszinierenden Tiere kennenzulernen und mehr über ihre Lebensweise zu erfahren." ]
                    , paragraph [] [ text "Bei uns dreht sich alles um das Wohlergehen unserer flauschigen Freunde. Von ihrer Pflege bis hin zur Haltung legen wir großen Wert darauf, dass sich unsere Alpakas rundum wohl fühlen." ]
                    , paragraph [] [ text "Wir laden Sie ein, unsere Webseite zu erkunden und mehr über unsere Arbeit und unsere liebevollen Gefährten zu erfahren. Falls Sie Fragen haben oder sich für bestimmte Aspekte interessieren, zögern Sie nicht, uns zu kontaktieren." ]
                    , paragraph [] [ text "Willkommen auf unserer Alpaka-Farm – wir freuen uns darauf, Ihnen unsere Welt näherzubringen!" ]
                    ]
                ]
            ]
        , el [ padding 50 ] <| none

        -- , html <|
        --     svg [ SvgAttributes.viewBox "0 0 100 100" ]
        --         [ Svg.path [ SvgAttributes.d "M 10 10 L 90 90" ] []
        --         , Svg.path [ SvgAttributes.d "M 10 10 L 90 90" ] []
        --         ]
        ]
    }
