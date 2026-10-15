# Screenshot tooling

The images in `assets/screens` are real captures of Cardscape running on its own demo seed
("Nexora Studios": fictional people with `@nexora.example` addresses, no real data).

Requirements: .NET 10 SDK, Python 3 with Playwright and Pillow, Google Chrome.

```bash
# 1. Run the API on a throw-away SQLite database (outside the repo)
export ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5291 Database__Provider=Sqlite
export ConnectionStrings__Default="Data Source=/tmp/cs-demo/demo.db" Storage__LocalRoot=/tmp/cs-demo/store
export Cardscape__DataProtection__KeyDirectory=/tmp/cs-demo/keys Cardscape__Database__RunMigrationsOnStartup=true
dotnet run --project src/Cardscape.Api

# 2. Plant the demo company (anonymous, only on an empty instance)
curl -X POST http://localhost:5291/api/setup/demo      # poll /api/setup/demo/status until "Succeeded"

# 3. Capture English and Spanish
python site/tools/showcase/capture.py /tmp/cs-shots/en en
python site/tools/showcase/capture.py /tmp/cs-shots/es es

# 4. Convert to WebP (1440x900 + 320x200 thumbnails) into site/assets/screens as <lang>-<name>.webp
```

`capture.py` signs in with the public demo account that the seeder always plants.
