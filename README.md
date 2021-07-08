# BoxToTabletop

This project helps you log & track your model backlog, from building through painting and being tabletop ready. If you have a pile of boxes or a collection of sprues, this project helps you manage that madness and watch your progress as you work through it.

TODO:
* Mobile layout
  * During this, add the default to switches and the option to configure to #s
  * show error messages appropriately
  * resize toast
  * work on data flow - should select a project immediately, or take you to projects list, no blank page
  * work on visible options - should not let you view settings if you do not have an active project
    * should also let you see unit list without going through "select project"
* Summary
  * By points/power/models
* Boil up errors to handle appropriately
  * timed out logins should try to re-log-in, not fail silently
* Add `?` tooltips
  * Don't use hover, not mobile friendly
* fix drag & drop to move units
  * add an Action menu to units to handle moving, deletion for mobile
* Dark mode
  * This is part of overall branding & design, looking for a designer
* Save units as one giant data batch, or save priorities better, or just improve this system SOMEHOW
  * avro? super condensed info transfer
  * Drop the `unit priorities` table if we're not using it.
* tags for units
    * filter on tags
* Release process in `build.fsx` should at least update release notes & create github release
  * There should be a link on the site for current version(s) & link to release notes
  * This project should really use a single condensed version, I think, though separate docker images maybe makes that not a good idea
* Custom columns
* projects & categories
    * Tree view with N levels, N options at root
    * page loader? this only seems to cover the full page, i want it on just the main project panel(s)
* Mark some things as currently in progress
* Move all domain mapping out of the repository
  * Create separate Domain layer to handle 80% of what the endpoints are handling
* account settings
    * quick view
    * customize auto save timer
    * name (changeable?)
      - does this matter if we having sharing links only? only matters if we end up with a "friends" system
* created & updated timestamps
    * set your own start, end dates
* sharing links
    * add friends
    * set account private
* next 10 - a way to group the next 10 things you want to work on, across multiple projects
* stats
    * by date, editable, with graphs
* export to CSV
* timelines w/ goals, show progress
* Fider for feedback

minor changes to make:
* Summary

far future:
* integrate math
* integrate games/crusade tracker
  * games: 
    * track : army played, opponent army, opponent, date, win/loss, victory points (objectives/secondaries/turn), mission, game point value
            * freeform text box for army list?
  * crusade:
    * track: games (linked w/ above), roster, XP, abilities
      * _loose_ - this isn't rebuilding Crusade, this is free-text-enter that's easier & faster than excel.

---

## Builds


GitHub Actions |
:---: |
[![GitHub Actions](https://github.com/Paige.M89/BoxToTabletop/workflows/Build%20master/badge.svg)](https://github.com/Paige.M89/BoxToTabletop/actions?query=branch%3Amaster) |
[![Build History](https://buildstats.info/github/chart/Paige.M89/BoxToTabletop)](https://github.com/Paige.M89/BoxToTabletop/actions?query=branch%3Amaster) |

## NuGet

Package | Stable | Prerelease
--- | --- | ---
BoxToTabletop | [![NuGet Badge](https://buildstats.info/nuget/BoxToTabletop)](https://www.nuget.org/packages/BoxToTabletop/) | [![NuGet Badge](https://buildstats.info/nuget/BoxToTabletop?includePreReleases=true)](https://www.nuget.org/packages/BoxToTabletop/)


---

### Developing

Make sure the following **requirements** are installed on your system:

- [dotnet SDK](https://www.microsoft.com/net/download/core) 3.0 or higher
- [Mono](http://www.mono-project.com/) if you're on Linux or macOS.

or

- [VSCode Dev Container](https://code.visualstudio.com/docs/remote/containers)


---

### Environment Variables

- `CONFIGURATION` will set the [configuration](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-build?tabs=netcore2x#options) of the dotnet commands.  If not set, it will default to Release.
  - `CONFIGURATION=Debug ./build.sh` will result in `-c` additions to commands such as in `dotnet build -c Debug`
- `GITHUB_TOKEN` will be used to upload release notes and NuGet packages to GitHub.
  - Be sure to set this before releasing
- `DISABLE_COVERAGE` Will disable running code coverage metrics.  AltCover can have [severe performance degradation](https://github.com/SteveGilham/altcover/issues/57) so it's worth disabling when looking to do a quicker feedback loop.
  - `DISABLE_COVERAGE=1 ./build.sh`


---

### Building


```sh
> build.cmd <optional buildtarget> // on windows
$ ./build.sh  <optional buildtarget>// on unix
```

---

### Build Targets


- `Clean` - Cleans artifact and temp directories.
- `DotnetRestore` - Runs [dotnet restore](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-restore?tabs=netcore2x) on the [solution file](https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/solution-dot-sln-file?view=vs-2019).
- [`DotnetBuild`](#Building) - Runs [dotnet build](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-build?tabs=netcore2x) on the [solution file](https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/solution-dot-sln-file?view=vs-2019).
- `DotnetTest` - Runs [dotnet test](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test?tabs=netcore21) on the [solution file](https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/solution-dot-sln-file?view=vs-2019.).
- `GenerateCoverageReport` - Code coverage is run during `DotnetTest` and this generates a report via [ReportGenerator](https://github.com/danielpalme/ReportGenerator).
- `WatchApp` - Runs [dotnet watch](https://docs.microsoft.com/en-us/aspnet/core/tutorials/dotnet-watch?view=aspnetcore-3.0) on the application. Useful for rapid feedback loops.
- `WatchTests` - Runs [dotnet watch](https://docs.microsoft.com/en-us/aspnet/core/tutorials/dotnet-watch?view=aspnetcore-3.0) with the test projects. Useful for rapid feedback loops.
- `GenerateAssemblyInfo` - Generates [AssemblyInfo](https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualbasic.applicationservices.assemblyinfo?view=netframework-4.8) for libraries.
- `CreatePackages` - Runs the packaging task from [dotnet-packaging](https://github.com/qmfrederik/dotnet-packaging). This creates applications for `win-x64`, `osx-x64` and `linux-x64` - [Runtime Identifiers](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog).  
    - Bundles the `win-x64` application in a .zip file.
    - Bundles the `osx-x64` application in a .tar.gz file.
    - Bundles the `linux-x64` application in a .tar.gz file.
- `GitRelease` - Creates a commit message with the [Release Notes](https://fake.build/apidocs/v5/fake-core-releasenotes.html) and a git tag via the version in the `Release Notes`.
- `GitHubRelease` - Publishes a [GitHub Release](https://help.github.com/en/articles/creating-releases) with the Release Notes and any NuGet packages.
- `FormatCode` - Runs [Fantomas](https://github.com/fsprojects/fantomas) on the solution file.
- [`Release`](#Releasing) - Task that runs all release type tasks such as `GitRelease` and `GitHubRelease`. Make sure to read [Releasing](#Releasing) to setup your environment correctly for releases.

---


### Releasing

#### Create docker images of the published versions of the projects

Note that the Client command needs to be run in the directory with the `package.json` file, so you still need to CD to that directory

```bash
./src/BoxToTabletop.Client/create-docker-image.sh
./src/BoxToTabletop/create-docker-image.sh
```

#### Tag images with the private repository

These instructions are for Digital Ocean, consult your cloud provider if hosting this project yourself somewhere else.

```bash
doctl registry login
docker tag registry.digitalocean.com/<my-registry>/<btt-client/btt-server>:<version>
docker tag registry.digitalocean.com/<my-registry>/<btt-client/btt-server>:latest
docker push registry.digitalocean.com/<my-registry>/<btt-client/btt-server>:<version>
docker push registry.digitalocean.com/<my-registry>/<btt-client/btt-server>:latest
```

Or just run `./tagAndPush.sh <image tag> <version>` in the server/client folders.

#### Kubernetes

Deploy changes via the files in `k8s` with `kubectl apply -f <filename>`. 

Find your running services with `kubectl get pods`.

If you're on Digital Ocean with a Load Balancer, find your ports under `Services` -> `ingress-nginx-controller` (note that the ports are harder to find on the resource page itself, just check the internal endpoints on the table).

#### Resources

Note that TLS termination was done at the laod balancer in Digital Ocean so none of these were needed after all.

* [Cert-manager for Kubernetes](https://github.com/jetstack/cert-manager)
  * Installed via `kubectl apply -f https://github.com/jetstack/cert-manager/releases/download/v1.3.1/cert-manager.yaml`. This is a massive file & it's impossible to verify everything in it.
  * Also install the `cert-manager` kubectl plugin [here](https://cert-manager.io/docs/usage/kubectl-plugin/). The config seems to merely create custom resource definitions, and not start anything, despite what the documentation says.
* [Let's Encrypt On K8s](https://runnable.com/blog/how-to-use-lets-encrypt-on-kubernetes) - this seems out of date

### Original miniscaffold release notes below

- [Start a git repo with a remote](https://help.github.com/articles/adding-an-existing-project-to-github-using-the-command-line/)

```sh
git add .
git commit -m "Scaffold"
git remote add origin https://github.com/user/MyCoolNewApp.git
git push -u origin master
```

- [Create a GitHub OAuth Token](https://help.github.com/articles/creating-a-personal-access-token-for-the-command-line/)
  - You can then set the `GITHUB_TOKEN` to upload release notes and artifacts to github
  - Otherwise it will fallback to username/password

- Then update the `CHANGELOG.md` with an "Unreleased" section containing release notes for this version, in [KeepAChangelog](https://keepachangelog.com/en/1.1.0/) format.


NOTE: Its highly recommend to add a link to the Pull Request next to the release note that it affects. The reason for this is when the `RELEASE` target is run, it will add these new notes into the body of git commit. GitHub will notice the links and will update the Pull Request with what commit referenced it saying ["added a commit that referenced this pull request"](https://github.com/TheAngryByrd/MiniScaffold/pull/179#ref-commit-837ad59). Since the build script automates the commit message, it will say "Bump Version to x.y.z". The benefit of this is when users goto a Pull Request, it will be clear when and which version those code changes released. Also when reading the `CHANGELOG`, if someone is curious about how or why those changes were made, they can easily discover the work and discussions.



Here's an example of adding an "Unreleased" section to a `CHANGELOG.md` with a `0.1.0` section already released.

```markdown
## [Unreleased]

### Added
- Does cool stuff!

### Fixed
- Fixes that silly oversight

## [0.1.0] - 2017-03-17
First release

### Added
- This release already has lots of features

[Unreleased]: https://github.com/user/MyCoolNewApp.git/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/user/MyCoolNewApp.git/releases/tag/v0.1.0
```

- You can then use the `Release` target, specifying the version number either in the `RELEASE_VERSION` environment
  variable, or else as a parameter after the target name.  This will:
  - update `CHANGELOG.md`, moving changes from the `Unreleased` section into a new `0.2.0` section
    - if there were any prerelease versions of 0.2.0 in the changelog, it will also collect their changes into the final 0.2.0 entry
  - make a commit bumping the version:  `Bump version to 0.2.0` and adds the new changelog section to the commit's body
  - push a git tag
  - create a GitHub release for that git tag


macOS/Linux Parameter:

```sh
./build.sh Release 0.2.0
```

macOS/Linux Environment Variable:

```sh
RELEASE_VERSION=0.2.0 ./build.sh Release
```


