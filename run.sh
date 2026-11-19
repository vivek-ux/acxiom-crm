#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
cd "$DIR/AcxiomCRM"

export PATH="/opt/homebrew/bin:$PATH"
export DOTNET_ROOT="/opt/homebrew/opt/dotnet/libexec"

echo "=========================================================="
echo "  🚀 Starting AcxiomCRM Enterprise Platform"
echo "=========================================================="
echo "  Database: SQLite (acxiomcrm.db - auto-seeded)"
echo "  Port:     http://localhost:5072"
echo "----------------------------------------------------------"
echo "  Pre-seeded Roles & Credentials:"
echo "    1. Admin:           admin@acxiom.com   / Admin@123"
echo "    2. Manager:         manager@acxiom.com / Manager@123"
echo "    3. Sales Executive: sales@acxiom.com   / Sales@123"
echo "=========================================================="

dotnet run --urls "http://localhost:5072"
