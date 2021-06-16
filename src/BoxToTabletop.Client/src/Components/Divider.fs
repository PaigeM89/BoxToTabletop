namespace Extensions.CreativeBulma

open Fulma
open Fable.React
open Fable.React.Props

module Divider =
  type DividerOption =
  | IsVertical
  | Color of IColor
  | Props of IHTMLProp list
  | CustomClass of string
  | Modifiers of Modifier.IModifier list

  let divider (options: DividerOption list) children =
    let parseOption (result : GenericOptions) (option : DividerOption) =
      match option with
      | IsVertical -> result.AddClass("is-vertical")
      | Color color -> ofColor color |> result.AddClass
      | Props props -> result.AddProps props
      | CustomClass customClass -> result.AddClass customClass
      | Modifiers modifiers -> result.AddModifiers modifiers

    GenericOptions.Parse(options, parseOption, "divider").ToReactElement(div, children)