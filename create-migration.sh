#!/usr/bin/env sh

set +eux

function usage () {
    echo "usage: create-migration \"description of migration\""
    exit 1;
}

[[ -z $1 || $# -gt 1 ]] && usage

description=${1// /_}

year=$(date "+%Y")
month=$(date "+%m")
day=$(date "+%d")
hour=$(date "+%H")
minute=$(date "+%M")
seconds=$(date "+%S")

repo_root=$(git rev-parse --show-toplevel)

timestamp=${year}_${month}_${day}_${hour}_${minute}_${seconds}
migration_name=${timestamp}_${description}
file_name=${repo_root}/src/BoxToTabletop/Migrations/${migration_name}.fs
class_name=_${migration_name}

echo "Writing new migration to ${file_name}"

cat >"${file_name}" <<EOF
namespace BoxToTabletop.Migrations

open FluentMigrator

[<Migration(${timestamp}L)>]
type ${class_name} () =
  inherit Migration ()

  override __.Up () = ()
  override __.Down () = ()
EOF

project_file=${repo_root}/src/BoxToTabletop/BoxToTabletop.fsproj

# trigger a reload to make tooling happy
touch "${project_file}"
