APP_PROJECT := src/TodoList.App/TodoList.App.csproj
SOLUTION := TodoList.slnx

CONFIG ?= Release
RID ?= win-x64
PUBLISH_ROOT ?= artifacts/publish
APP_NAME ?= todolist

SINGLE_FILE_OUT ?= $(PUBLISH_ROOT)/$(RID)-single-$(APP_NAME)
FRAMEWORK_OUT ?= $(PUBLISH_ROOT)/$(RID)-framework-$(APP_NAME)

.PHONY: help restore build publish publish-single publish-framework clean-publish

help:
	@echo "Available targets:"
	@echo "  make restore                 Restore NuGet packages"
	@echo "  make build                   Build solution (CONFIG=Release by default)"
	@echo "  make publish                 Alias for publish-single"
	@echo "  make publish-single          Publish self-contained single-file build"
	@echo "  make publish-framework       Publish framework-dependent build"
	@echo "  make clean-publish           Remove publish output folder"
	@echo ""
	@echo "Optional overrides:"
	@echo "  CONFIG=Release|Debug RID=win-x64 PUBLISH_ROOT=artifacts/publish"

restore:
	dotnet restore $(SOLUTION)

build:
	dotnet build $(SOLUTION) -c $(CONFIG)

publish: publish-single

publish-single: restore
	dotnet publish $(APP_PROJECT) -c $(CONFIG) -r $(RID) --self-contained true \
		-p:PublishSingleFile=true \
		-p:IncludeNativeLibrariesForSelfExtract=true \
		-p:EnableCompressionInSingleFile=true \
		-p:PublishTrimmed=false \
		-p:DebugType=None \
		-p:DebugSymbols=false \
		-o $(SINGLE_FILE_OUT)

publish-framework: restore
	dotnet publish $(APP_PROJECT) -c $(CONFIG) -r $(RID) --self-contained false \
		-p:PublishSingleFile=false \
		-o $(FRAMEWORK_OUT)

clean-publish:
ifeq ($(OS),Windows_NT)
	@if exist "$(PUBLISH_ROOT)" rmdir /s /q "$(PUBLISH_ROOT)"
else
	rm -rf $(PUBLISH_ROOT)
endif