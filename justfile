# generate a local private key for development purposes
generate-private-key:
    openssl genrsa -out private.pem 2048

# build native video library for macOS (arm64)
build-native-macos:
    cmake -S native -B native/build-macos -DCMAKE_BUILD_TYPE=Release
    cmake --build native/build-macos
    cp native/build-macos/macos/libfennec_video.dylib Fennec.App/runtimes/osx-arm64/native/

# build native video library for Linux (x64)
build-native-linux:
    cmake -S native -B native/build-linux -DCMAKE_BUILD_TYPE=Release
    cmake --build native/build-linux
    cp native/build-linux/linux/libfennec_video.so Fennec.App/runtimes/linux-x64/native/

# build native video library for Windows (x64)
build-native-windows:
    cmake -S native -B native/build-windows -DCMAKE_BUILD_TYPE=Release
    cmake --build native/build-windows --config Release
    cp native/build-windows/windows/Release/fennec_video.dll Fennec.App/runtimes/win-x64/native/
