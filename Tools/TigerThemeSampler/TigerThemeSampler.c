#include <Carbon/Carbon.h>
#include <ApplicationServices/ApplicationServices.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define SHEET_WIDTH 240
#define SHEET_HEIGHT 336
#define BUTTON_X 32
#define BUTTON_WIDTH 176
#define BUTTON_HEIGHT 20
#define ROW_HEIGHT 48
#define ROW_COUNT 7

struct Sample {
	const char *name;
	ThemeDrawState state;
	ThemeButtonAdornment adornment;
};

static const struct Sample samples[ROW_COUNT] = {
	{ "normal",   kThemeStateActive,              kThemeAdornmentNone },
	{ "default",  kThemeStateActive,              kThemeAdornmentDefault },
	{ "pressed",  kThemeStatePressed,             kThemeAdornmentNone },
	{ "focused",  kThemeStateActive,              kThemeAdornmentFocus },
	{ "inactive", kThemeStateInactive,            kThemeAdornmentNone },
	{ "disabled", kThemeStateUnavailable,         kThemeAdornmentNone },
	{ "disabled-inactive", kThemeStateUnavailableInactive, kThemeAdornmentNone }
};

static OSStatus DrawSample(CGContextRef context, const struct Sample *sample, float topY)
{
	HIRect bounds;
	HIThemeButtonDrawInfo info;

	bounds.origin.x = BUTTON_X;
	bounds.origin.y = SHEET_HEIGHT - topY - BUTTON_HEIGHT;
	bounds.size.width = BUTTON_WIDTH;
	bounds.size.height = BUTTON_HEIGHT;

	info.version = 0;
	info.state = sample->state;
	info.kind = kThemePushButton;
	info.value = kThemeButtonOff;
	info.adornment = sample->adornment;
	info.animation.frame.index = 0;

	return HIThemeDrawButton(&bounds, &info, context,
							 kHIThemeOrientationNormal, NULL);
}

static int WriteTGARect(CGContextRef context, const char *path,
						int topX, int topY, int rectWidth, int rectHeight)
{
	unsigned char *data = (unsigned char *)CGBitmapContextGetData(context);
	size_t bytesPerRow = CGBitmapContextGetBytesPerRow(context);
	size_t contextHeight = CGBitmapContextGetHeight(context);
	FILE *file = fopen(path, "wb");
	unsigned char header[18];
	int y;

	if (file == NULL || data == NULL || rectWidth <= 0 || rectHeight <= 0)
		return 1;

	memset(header, 0, sizeof(header));
	header[2] = 2;
	header[12] = (unsigned char)(rectWidth & 0xff);
	header[13] = (unsigned char)((rectWidth >> 8) & 0xff);
	header[14] = (unsigned char)(rectHeight & 0xff);
	header[15] = (unsigned char)((rectHeight >> 8) & 0xff);
	header[16] = 32;
	header[17] = 8 | 0x20;
	fwrite(header, 1, sizeof(header), file);

	for (y = 0; y < rectHeight; ++y) {
		int x;
		unsigned char *row = data + (contextHeight - topY - y - 1) * bytesPerRow;
		for (x = 0; x < rectWidth; ++x) {
			unsigned char *pixel = row + (topX + x) * 4;
			unsigned char alpha = pixel[3];
			unsigned char red = alpha ? (unsigned char)((pixel[0] * 255U + alpha / 2) / alpha) : 0;
			unsigned char green = alpha ? (unsigned char)((pixel[1] * 255U + alpha / 2) / alpha) : 0;
			unsigned char blue = alpha ? (unsigned char)((pixel[2] * 255U + alpha / 2) / alpha) : 0;
			fputc(blue, file);
			fputc(green, file);
			fputc(red, file);
			fputc(alpha, file);
		}
	}

	fclose(file);
	return 0;
}

static int WriteTGA(CGContextRef context, const char *path)
{
	unsigned char *data = (unsigned char *)CGBitmapContextGetData(context);
	size_t bytesPerRow = CGBitmapContextGetBytesPerRow(context);
	size_t width = CGBitmapContextGetWidth(context);
	size_t height = CGBitmapContextGetHeight(context);
	FILE *file = fopen(path, "wb");
	unsigned char header[18];
	size_t y;

	if (file == NULL || data == NULL || width == 0 || height == 0)
		return 1;

	memset(header, 0, sizeof(header));
	header[2] = 2;
	header[12] = (unsigned char)(width & 0xff);
	header[13] = (unsigned char)((width >> 8) & 0xff);
	header[14] = (unsigned char)(height & 0xff);
	header[15] = (unsigned char)((height >> 8) & 0xff);
	header[16] = 32;
	header[17] = 8 | 0x20;
	fwrite(header, 1, sizeof(header), file);

	for (y = 0; y < height; ++y) {
		size_t x;
		unsigned char *row = data + (height - y - 1) * bytesPerRow;
		for (x = 0; x < width; ++x) {
			unsigned char *pixel = row + x * 4;
			unsigned char rgba[4];
			unsigned int alpha = pixel[3];

			if (alpha == 0) {
				rgba[0] = rgba[1] = rgba[2] = 0;
			} else {
				rgba[0] = (unsigned char)((pixel[0] * 255U + alpha / 2) / alpha);
				rgba[1] = (unsigned char)((pixel[1] * 255U + alpha / 2) / alpha);
				rgba[2] = (unsigned char)((pixel[2] * 255U + alpha / 2) / alpha);
			}
			rgba[3] = (unsigned char)alpha;
			fputc(rgba[2], file);
			fputc(rgba[1], file);
			fputc(rgba[0], file);
			fputc(rgba[3], file);
		}
	}

	fclose(file);
	return 0;
}

int main(int argc, char **argv)
{
	const char *output = (argc > 1) ? argv[1] : "TigerThemeSampler.tga";
	const char *metadata = (argc > 2) ? argv[2] : "TigerThemeSampler.tsv";
	const char *stateDirectory = (argc > 4) ? argv[4] : NULL;
	int scale = (argc > 3) ? atoi(argv[3]) : 1;
	size_t width;
	size_t height;
	size_t bytesPerRow;
	void *bitmapData;
	CGColorSpaceRef colorSpace = CGColorSpaceCreateDeviceRGB();
	CGContextRef context;
	FILE *file;
	int i;
	int result;

	if (scale < 1) scale = 1;
	width = SHEET_WIDTH * scale;
	height = SHEET_HEIGHT * scale;
	bytesPerRow = width * 4;
	bitmapData = calloc(height, bytesPerRow);
	context = CGBitmapContextCreate(bitmapData, width, height, 8, bytesPerRow,
																		 colorSpace, kCGImageAlphaPremultipliedLast);

	if (context == NULL || colorSpace == NULL) {
		fprintf(stderr, "Unable to create bitmap context\n");
		return 1;
	}

	CGContextClearRect(context, CGRectMake(0, 0, width, height));
	CGContextScaleCTM(context, scale, scale);

	for (i = 0; i < ROW_COUNT; ++i) {
		OSStatus status;
		status = DrawSample(context, &samples[i], i * ROW_HEIGHT + 12);
		if (status != noErr)
			fprintf(stderr, "%s: HIThemeDrawButton returned %ld\n",
					samples[i].name, (long)status);
	}

	result = WriteTGA(context, output);
	if (stateDirectory != NULL) {
		char path[1024];
		for (i = 0; i < ROW_COUNT; ++i) {
			snprintf(path, sizeof(path), "%s/%s.tga", stateDirectory, samples[i].name);
			if (WriteTGARect(context, path, BUTTON_X, i * ROW_HEIGHT + 12,
							 BUTTON_WIDTH, BUTTON_HEIGHT) != 0)
				result = 1;
		}
	}

	file = fopen(metadata, "w");
	if (file != NULL) {
		fprintf(file, "# scale\t%d\n", scale);
		fprintf(file, "# name\tstate\tadornment\tx\ty\twidth\theight\n");
		for (i = 0; i < ROW_COUNT; ++i)
			fprintf(file, "%s\t%lu\t%lu\t%d\t%d\t%d\t%d\n",
					samples[i].name, (unsigned long)samples[i].state,
					(unsigned long)samples[i].adornment, BUTTON_X,
					(int)(i * ROW_HEIGHT + 12), BUTTON_WIDTH, BUTTON_HEIGHT);
		fclose(file);
	} else {
		fprintf(stderr, "Unable to write metadata file: %s\n", metadata);
		result = 1;
	}

	CGContextRelease(context);
	CGColorSpaceRelease(colorSpace);
	free(bitmapData);
	return result;
}
