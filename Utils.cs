using System.Drawing;

namespace MNML 
{
    public class Utils
    {
        public static Size ScaleDimensions(Size size, int maxSize)
        {
            int originalWidth = size.Width;
            int originalHeight = size.Height;
            double aspectRatio = (double)originalWidth / originalHeight;

            int newWidth, newHeight;

            if (originalWidth > originalHeight)
            {
                newWidth = maxSize;
                newHeight = (int)(newWidth / aspectRatio);
            }
            else
            {
                newHeight = maxSize;
                newWidth = (int)(newHeight * aspectRatio);
            }


            return new Size(newWidth, newHeight);
        }

    }

}