#!/bin/bash

# install-wasm-sdk.sh
# Downloads and installs WASI SDK 28

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
WASI_SDK_VERSION="28.0"
WASI_SDK_URL="https://github.com/WebAssembly/wasi-sdk/releases/download/wasi-sdk-28"
INSTALL_DIR="${HOME}/.wasi-sdk"

# Detect OS and architecture
detect_platform() {
    local os=$(uname -s)
    local arch=$(uname -m)
    
    case "${os}_${arch}" in
        Darwin_arm64)
            echo "wasi-sdk-${WASI_SDK_VERSION}-arm64-macos.tar.gz"
            ;;
        Darwin_x86_64)
            echo "wasi-sdk-${WASI_SDK_VERSION}-x86_64-macos.tar.gz"
            ;;
        Linux_x86_64)
            echo "wasi-sdk-${WASI_SDK_VERSION}-x86_64-linux.tar.gz"
            ;;
        Linux_aarch64)
            echo "wasi-sdk-${WASI_SDK_VERSION}-arm64-linux.tar.gz"
            ;;
        *)
            echo ""
            return 1
            ;;
    esac
}

# Main installation
main() {
    echo -e "${GREEN}WASI SDK ${WASI_SDK_VERSION} Installer${NC}"
    echo ""
    
    # Detect platform
    echo -e "${YELLOW}Detecting platform...${NC}"
    local archive=$(detect_platform)
    
    if [ -z "$archive" ]; then
        echo -e "${RED}Error: Unsupported platform${NC}"
        echo "This script supports:"
        echo "  - macOS ARM64 (Apple Silicon)"
        echo "  - macOS x86_64 (Intel)"
        echo "  - Linux x86_64"
        echo "  - Linux ARM64"
        exit 1
    fi
    
    echo -e "${GREEN}✓ Detected: $archive${NC}"
    echo ""
    
    # Create install directory
    echo -e "${YELLOW}Creating installation directory: ${INSTALL_DIR}${NC}"
    mkdir -p "${INSTALL_DIR}"
    
    # Download SDK
    local download_url="${WASI_SDK_URL}/${archive}"
    local temp_file="${INSTALL_DIR}/${archive}"
    
    echo -e "${YELLOW}Downloading WASI SDK from:${NC}"
    echo "  ${download_url}"
    echo ""
    
    if command -v curl &> /dev/null; then
        echo -e "${YELLOW}Using curl for download...${NC}"
        curl -L -f -o "${temp_file}" "${download_url}" || {
            echo -e "${RED}Error: curl failed to download the file${NC}"
            echo "URL: ${download_url}"
            exit 1
        }
    elif command -v wget &> /dev/null; then
        echo -e "${YELLOW}Using wget for download...${NC}"
        wget -O "${temp_file}" "${download_url}" || {
            echo -e "${RED}Error: wget failed to download the file${NC}"
            echo "URL: ${download_url}"
            exit 1
        }
    else
        echo -e "${RED}Error: Neither curl nor wget found${NC}"
        exit 1
    fi
    
    if [ ! -f "${temp_file}" ]; then
        echo -e "${RED}Error: Failed to download WASI SDK${NC}"
        exit 1
    fi
    
    # Verify file is gzip
    echo -e "${YELLOW}Verifying downloaded file...${NC}"
    local file_type=$(file "${temp_file}")
    if ! echo "${file_type}" | grep -q "gzip"; then
        echo -e "${RED}Error: Downloaded file is not a gzip archive${NC}"
        echo "File type: ${file_type}"
        echo ""
        echo -e "${YELLOW}This usually means:${NC}"
        echo "  1. The download URL is incorrect or has redirected"
        echo "  2. GitHub rate limiting may be blocking the download"
        echo "  3. The release may not exist for your platform"
        echo ""
        echo -e "${YELLOW}Try downloading manually from:${NC}"
        echo "  https://github.com/WebAssembly/wasi-sdk/releases/tag/v${WASI_SDK_VERSION}"
        rm -f "${temp_file}"
        exit 1
    fi
    echo -e "${GREEN}✓ Downloaded${NC}"
    echo ""
    
    # Extract SDK
    echo -e "${YELLOW}Extracting WASI SDK...${NC}"
    if ! tar -xzf "${temp_file}" -C "${INSTALL_DIR}"; then
        echo -e "${RED}Error: Failed to extract archive${NC}"
        rm -f "${temp_file}"
        exit 1
    fi
    rm -f "${temp_file}"
    
    # Find the extracted directory
    local sdk_dir=$(ls -d "${INSTALL_DIR}"/wasi-sdk-* 2>/dev/null | head -1)
    if [ -z "$sdk_dir" ]; then
        echo -e "${RED}Error: Could not find extracted SDK${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}✓ Extracted to: ${sdk_dir}${NC}"
    echo ""
    
    # macOS: Remove quarantine attributes
    if [[ "$OSTYPE" == "darwin"* ]]; then
        echo -e "${YELLOW}Removing macOS quarantine attributes...${NC}"
        xattr -rd com.apple.quarantine "${sdk_dir}" 2>/dev/null || true
        echo -e "${GREEN}✓ Done${NC}"
        echo ""
    fi
    
    # Output environment variable
    echo -e "${GREEN}Installation complete!${NC}"
    echo ""
    echo -e "${YELLOW}To use the SDK, set the environment variable:${NC}"
    echo ""
    echo -e "${GREEN}export WASI_SDK_PATH=\"${sdk_dir}\"${NC}"
    echo ""
    echo -e "${YELLOW}To make this permanent, add the following to your shell profile${NC}"
    echo -e "${YELLOW}(~/.bash_profile, ~/.zshrc, etc.):${NC}"
    echo ""
    echo "export WASI_SDK_PATH=\"${sdk_dir}\""
    echo ""
}

main "$@"
