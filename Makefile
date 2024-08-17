.PHONY: all clean build yak spec update-manifest build-yak

YAK_PATH := /Applications/Rhino\ 8.app/Contents/Resources/bin/yak
RELEASE_DIR := bin/release/net7.0
GITHUB_REPO_NAME := $(shell basename $(PWD))
PROJECT_FILE := RhFspy.csproj
YAK_FILE := $(shell find $(RELEASE_DIR) -name "rhfspy-*.yak" | sort -V | tail -n 1)

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
		$(YAK_PATH) spec || \
		$(YAK_PATH) build --platform any
	@echo "Yak build completed."

publish: yak
	@if [ -z "$(YAK_FILE)" ]; then \
		echo "Error: No .yak file found in $(RELEASE_DIR)"; \
		exit 1; \
	fi
	@echo "Publishing Yak file: $(YAK_FILE)"
	@cd $(RELEASE_DIR) && \
	$(YAK_PATH) push $(notdir $(YAK_FILE))