# H-Nice
A C# packet logger & manipulator interface for Habbo Origin, inspired in the old SnG Fun v1 written in VB6. It's still in a very early stage, so don't despair.

## Usage

You have to connect to the Habbo Origins server you want, choosing the necessary DNS and pinging it to find out the default IP address of the game. Then click *connect* and logging into your personal Origin's account through the official launcher and you are done. Enjoy the sockets' world!

## Building from source

Requires the .NET 8 SDK.

```sh
# Clone the repo
git clone https://github.com/Juanru9/HNice -b develop
# Build & run the application
dotnet build src/HNice -c Release -o bin\Release
cd bin\Release
.\HNice
```
