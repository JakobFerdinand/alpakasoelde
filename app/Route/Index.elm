module Route.Index exposing (ActionData, Data, Model, Msg, route)

import BackendTask exposing (BackendTask)
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
    { title = "Alpakasölde"
    , body =
        [ column [ spacing 20, centerY, width fill ]
            [ el [ centerX, Region.heading 1 ] (text "ALPAKASÖLDE")
            , el [ centerX ] (paragraph [ Font.center ] [ text "Schön, dass sie die Seite der Alpakasölde besuchen." ])

            -- , html <|
            --     svg [ SvgAttributes.viewBox "0 0 100 100" ]
            --         [ Svg.path [ SvgAttributes.d "M 10 10 L 90 90" ] []
            --         , Svg.path [ SvgAttributes.d "M 10 10 L 90 90" ] []
            --         ]
            ]
        ]
    }
