module Route.Greet exposing (ActionData, Data, Model, Msg, route)

import BackendTask exposing (BackendTask)
import BackendTask.Http
import ErrorPage exposing (ErrorPage)
import FatalError exposing (FatalError)
import Head
import Head.Seo as Seo
import Json.Decode as Decode
import Pages.Url
import PagesMsg exposing (PagesMsg)
import RouteBuilder exposing (App, StatefulRoute, StatelessRoute)
import Server.Request as Request exposing (Request)
import Server.Response as Response exposing (Response)
import Shared
import View exposing (View)
import Element


type alias Model =
    {}


type alias Msg =
    ()


type alias RouteParams =
    {}


route : StatelessRoute RouteParams Data ActionData
route =
    RouteBuilder.serverRender
        { head = head
        , data = data
        , action = \_ _ -> BackendTask.fail (FatalError.fromString "No action.")
        }
        |> RouteBuilder.buildNoState { view = view }


type alias Data =
    { name : Maybe String
    }


type alias ActionData =
    {}


data : RouteParams -> Request -> BackendTask FatalError (Response Data ErrorPage)
data routeParams request =
    case request |> Request.queryParam "name" of
        Just name ->
            BackendTask.Http.getJson "http://worldtimeapi.org/api/timezone/America/Los_Angeles"
                (Decode.field "utc_datetime" Decode.string)
                |> BackendTask.allowFatal
                |> BackendTask.map
                    (\dateTimeString ->
                        Response.render
                            { name = Just dateTimeString }
                    )

        Nothing ->
            BackendTask.succeed
                (Response.render
                    { name = Nothing }
                )


head :
    App Data ActionData RouteParams
    -> List Head.Tag
head app =
    Seo.summary
        { canonicalUrlOverride = Nothing
        , siteName = "elm-pages"
        , image =
            { url = Pages.Url.external "TODO"
            , alt = "elm-pages logo"
            , dimensions = Nothing
            , mimeType = Nothing
            }
        , description = "TODO"
        , locale = Nothing
        , title = "TODO title" -- metadata.title -- TODO
        }
        |> Seo.website


view :
    App Data ActionData RouteParams
    -> Shared.Model
    -> View (PagesMsg Msg)
view app shared =
    { title = "Greetings"
    , body =
        [ Element.column []
            [ case app.data.name of
                Just name ->
                    Element.text ("Hello " ++ name)

                Nothing ->
                    Element.text "Hello, I didn't find your name"
            ]
        ]
    }
