.PHONY: all clean build yak spec update-manifest build-yak

YAK_PATH := /Applications/Rhino\ 8.app/Contents/Resources/bin/yak
RELEASE_DIR := bin/release/net7.0
GITHUB_REPO_NAME := $(shell basename $(PWD))
PROJECT_FILE := RhFspy.csproj

all: clean build yak

clean:
	dotnet clean $(PROJECT_FILE)

dev:
	dotnet build -c Debug $(PROJECT_FILE)


test: dev
	RHINO_PLUGIN_PATH=./bin/Debug/net7.0/RhFspy.rhp /Applications/Rhino\ 8.app/Contents/MacOS/Rhinoceros

test.v7: dev
	RHINO_PLUGIN_PATH=./bin/Debug/net7.0/RhFspy.rhp /Applications/Rhino\ 7.app/Contents/MacOS/Rhinoceros

build:
	dotnet build -c release $(PROJECT_FILE)

yak: build
	@echo "Building with Yak..."
	@cd $(RELEASE_DIR) && \
		$(YAK_PATH) spec && \
		sed -i '' 's|<url>|https://github.com/mnmly/$(GITHUB_REPO_NAME)|g' manifest.yml && \
		$(YAK_PATH) build --platform any
	@echo "Yak build completed."

publish: yak
	@cd $(RELEASE_DIR) && \
	$(YAK_PATH) push