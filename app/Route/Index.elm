module Route.Index exposing (ActionData, Data, Model, Msg, route)

import BackendTask exposing (BackendTask)
import FatalError exposing (FatalError)
import Head
import Head.Seo as Seo
import Html.Styled as Html exposing (..)
import Html.Styled.Attributes as Attr exposing (css)
import Pages.Url
import PagesMsg exposing (PagesMsg)
import Route
import RouteBuilder exposing (App, StatelessRoute)
import Shared
import Tailwind.Utilities exposing (..)
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
        [ Html.div
            [ css
                [ grow
                , flex
                , flex_col
                , text_center
                ]
            ]
            [ Html.div [ css [ w_full, h_96, flex, justify_center ] ]
                [ Html.img
                    [ css [ object_fill, max_h_96 ]
                    , Attr.src "/alpakafarm.svg"
                    ]
                    []
                ]
            , Html.div [ css [] ]
                [ Html.div [ css [ font_bold, text_6xl ] ] [ Html.text "ALPAKASÖLDE" ]
                , Html.div [ css [ my_4 ] ]
                    [ Html.p [] [ text "Willkommen auf unserer Alpaka-Farm!" ]
                    , Html.p [] [ text "Wir freuen uns, dass Sie den Weg zu uns gefunden haben und möchten Sie in die zauberhafte Welt unserer Alpakas entführen." ]
                    , Html.p [] [ text "Unsere Farm ist ein Ort der Ruhe und des Staunens, wo Sie die Gelegenheit haben, die faszinierenden Tiere kennenzulernen und mehr über ihre Lebensweise zu erfahren." ]
                    , Html.p [] [ text "Bei uns dreht sich alles um das Wohlergehen unserer flauschigen Freunde. Von ihrer Pflege bis hin zur Haltung legen wir großen Wert darauf, dass sich unsere Alpakas rundum wohl fühlen." ]
                    , Html.p [] [ text "Wir laden Sie ein, unsere Webseite zu erkunden und mehr über unsere Arbeit und unsere liebevollen Gefährten zu erfahren. Falls Sie Fragen haben oder sich für bestimmte Aspekte interessieren, zögern Sie nicht, uns zu kontaktieren." ]
                    ]
                ]
            ]

        -- , html <|
        --     svg [ SvgAttributes.viewBox "0 0 100 100" ]
        --         [ Svg.path [ SvgAttributes.d "M 10 10 L 90 90" ] []
        --         , Svg.path [ SvgAttributes.d "M 10 10 L 90 90" ] []
        --         ]
        ]
    }
