module Site exposing (config)

import BackendTask exposing (BackendTask)
import FatalError exposing (FatalError)
import Head
import LanguageTag
import LanguageTag.Language
import MimeType
import Pages.Url as Url
import SiteConfig exposing (SiteConfig)


config : SiteConfig
config =
    { canonicalUrl = "https://alpakasoelde.at"
    , head = head
    }


head : BackendTask FatalError (List Head.Tag)
head =
    [ Head.metaName "viewport" (Head.raw "width=device-width,initial-scale=1")
    , Head.sitemapLink "/sitemap.xml"
    , LanguageTag.Language.de |> LanguageTag.build LanguageTag.emptySubtags |> Head.rootLanguage
    , Head.metaName "theme-color" (Head.raw "#ffffff")
    , Head.metaName "apple-mobile-web-app-capable" (Head.raw "yes")
    , Head.metaName "apple-mobile-web-app-status-bar-style" (Head.raw "black-translucent")
    , Head.icon [ ( 192, 192 ) ] MimeType.Png (Url.external "/android-chrome-192x192.png")
    , Head.icon [ ( 512, 512 ) ] MimeType.Png (Url.external "/android-chrome-512x512.png")
    , Head.appleTouchIcon (Just 20) (Url.external "/apple-touch-icon.png")
    ]
        |> BackendTask.succeed
