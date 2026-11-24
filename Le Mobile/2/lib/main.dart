import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();

  SystemChrome.setSystemUIOverlayStyle(
    const SystemUiOverlayStyle(
      statusBarColor: Colors.transparent,
      statusBarIconBrightness: Brightness.dark,
      statusBarBrightness: Brightness.light,
    ),
  );

  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return const MaterialApp(debugShowCheckedModeBanner: false, home: Page1());
  }
}

class Categories extends StatelessWidget {
  static const Map<String, Color> checkedColorScheme = {
    "font": Colors.white,
    "bgColor": Color(0xFF1F2427),
  };

  static const Map<String, Color> uncheckedColorScheme = {
    "font": Colors.black,
    "bgColor": Colors.white,
  };

  final bool checked;
  final String text;
  final String num;

  const Categories({
    super.key,
    required this.checked,
    required this.text,
    required this.num,
  });

  @override
  Widget build(BuildContext context) {
    final Color fontColor =
        (checked ? checkedColorScheme["font"] : uncheckedColorScheme["font"]) ??
        Colors.black;

    final Color bgColor =
        (checked
            ? checkedColorScheme["bgColor"]
            : uncheckedColorScheme["bgColor"]) ??
        Colors.white;

    return Padding(
      padding: const EdgeInsets.only(right: 16),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 29),
        decoration: BoxDecoration(
          color: bgColor,
          borderRadius: BorderRadius.circular(16),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(text, style: TextStyle(color: fontColor, fontSize: 19)),
            const SizedBox(width: 10),
            Text(
              num,
              style: TextStyle(color: fontColor.withOpacity(0.6), fontSize: 14),
            ),
          ],
        ),
      ),
    );
  }
}

class Carousel extends StatefulWidget {
  const Carousel({super.key});

  @override
  State<Carousel> createState() => _CarouselState();
}

class _CarouselState extends State<Carousel> {
  final PageController _pageController = PageController(viewportFraction: 0.88);

  int currentIndex = 0;

  final List<Map<String, dynamic>> hotels = [
    {
      "name": "Siargao Luxury Resort",
      "location": "Siargao Island, Philippines",
      "rating": 4.9,
      "price": 15.99,
      "image": "assets/hans-hotel.jpg",
    },
    {
      "name": "Tropical Paradise Hotel",
      "location": "Bali, Indonesia",
      "rating": 4.8,
      "price": 25.99,
      "image": "assets/tropics.jpg",
    },
    {
      "name": "Ocean View Resort",
      "location": "Maldives",
      "rating": 4.7,
      "price": 35.99,
      "image": "assets/beach.jpg",
    },
  ];

  @override
  void dispose() {
    _pageController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 440,
      child: PageView.builder(
        controller: _pageController,
        itemCount: hotels.length,
        onPageChanged: (index) {
          setState(() {
            currentIndex = index;
          });
        },
        itemBuilder: (context, index) {
          final hotel = hotels[index];

          final String name = hotel["name"] as String;
          final String location = hotel["location"] as String;
          final double rating = (hotel["rating"] as num).toDouble();
          final double price = (hotel["price"] as num).toDouble();
          final String image = hotel["image"] as String;

          return GestureDetector(
            onTap: () async {
              final result = await Navigator.push<int>(
                context,
                MaterialPageRoute(
                  builder: (context) => Page2(
                    name: name,
                    location: location,
                    rating: rating,
                    price: price,
                    image: image,
                    selectedIndex: index,
                  ),
                ),
              );

              if (!mounted) return;

              if (result != null) {
                setState(() {
                  currentIndex = result;
                });

                _pageController.animateToPage(
                  result,
                  duration: const Duration(milliseconds: 400),
                  curve: Curves.easeInOut,
                );

                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(
                    content: Text(
                      'Получен результат из Page2: индекс = $result, '
                      'отель = ${hotels[result]["name"]}',
                    ),
                    duration: const Duration(seconds: 3),
                  ),
                );
              }
            },
            child: Container(
              margin: const EdgeInsets.symmetric(horizontal: 5, vertical: 25),
              child: Stack(
                alignment: Alignment.topCenter,
                children: [
                  Positioned(
                    top: 165,
                    child: Container(
                      width: 280,
                      height: 270,
                      decoration: BoxDecoration(
                        color: Colors.grey[400],
                        borderRadius: BorderRadius.circular(32),
                      ),
                    ),
                  ),
                  Positioned(
                    top: 90,
                    child: Container(
                      width: 325,
                      height: 320,
                      decoration: BoxDecoration(
                        color: Colors.grey[200],
                        borderRadius: BorderRadius.circular(32),
                      ),
                    ),
                  ),
                  Container(
                    width: double.infinity,
                    height: 380,
                    padding: const EdgeInsets.all(15),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(32),
                      boxShadow: [
                        BoxShadow(
                          color: Colors.black.withOpacity(0.06),
                          blurRadius: 12,
                          spreadRadius: 2,
                          offset: const Offset(0, 5),
                        ),
                      ],
                    ),
                    child: ClipRRect(
                      borderRadius: BorderRadius.circular(28),
                      child: Stack(
                        fit: StackFit.expand,
                        children: [
                          Image.asset(image, fit: BoxFit.cover),
                          Positioned(
                            top: 16,
                            right: 16,
                            child: Container(
                              width: 50,
                              height: 50,
                              decoration: BoxDecoration(
                                color: Colors.white.withOpacity(0.95),
                                borderRadius: BorderRadius.circular(16),
                                boxShadow: [
                                  BoxShadow(
                                    color: Colors.black.withOpacity(0.18),
                                    blurRadius: 10,
                                    offset: const Offset(0, 4),
                                  ),
                                ],
                              ),
                              child: const Icon(
                                Icons.favorite,
                                color: Colors.pinkAccent,
                                size: 27,
                              ),
                            ),
                          ),
                          Positioned(
                            left: 12,
                            right: 12,
                            bottom: 12,
                            child: ClipRRect(
                              borderRadius: BorderRadius.circular(22),
                              child: BackdropFilter(
                                filter: ImageFilter.blur(
                                  sigmaX: 12,
                                  sigmaY: 12,
                                ),
                                child: Container(
                                  padding: const EdgeInsets.symmetric(
                                    horizontal: 18,
                                    vertical: 16,
                                  ),
                                  decoration: BoxDecoration(
                                    color: Colors.black.withOpacity(0.48),
                                    borderRadius: BorderRadius.circular(22),
                                    border: Border.all(
                                      color: Colors.white.withOpacity(0.15),
                                      width: 1,
                                    ),
                                  ),
                                  child: Row(
                                    crossAxisAlignment: CrossAxisAlignment.end,
                                    children: [
                                      Expanded(
                                        child: Column(
                                          mainAxisSize: MainAxisSize.min,
                                          crossAxisAlignment:
                                              CrossAxisAlignment.start,
                                          children: [
                                            Text(
                                              name,
                                              style: const TextStyle(
                                                color: Colors.white,
                                                fontSize: 20,
                                                fontWeight: FontWeight.w700,
                                                height: 1.2,
                                              ),
                                            ),
                                            const SizedBox(height: 10),
                                            Row(
                                              children: [
                                                const Icon(
                                                  Icons.location_on,
                                                  color: Colors.white70,
                                                  size: 17,
                                                ),
                                                const SizedBox(width: 5),
                                                Flexible(
                                                  child: Text(
                                                    location,
                                                    style: const TextStyle(
                                                      color: Colors.white70,
                                                      fontSize: 13,
                                                      fontWeight:
                                                          FontWeight.w500,
                                                      height: 1.3,
                                                    ),
                                                  ),
                                                ),
                                              ],
                                            ),
                                          ],
                                        ),
                                      ),
                                      const SizedBox(width: 10),
                                      Row(
                                        mainAxisSize: MainAxisSize.min,
                                        children: [
                                          Text(
                                            rating.toString(),
                                            style: const TextStyle(
                                              color: Colors.white,
                                              fontSize: 15,
                                              fontWeight: FontWeight.w700,
                                            ),
                                          ),
                                          const SizedBox(width: 4),
                                          const Icon(
                                            Icons.star,
                                            color: Colors.amber,
                                            size: 19,
                                          ),
                                        ],
                                      ),
                                    ],
                                  ),
                                ),
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}

class BottomMenu extends StatelessWidget {
  const BottomMenu({super.key});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 20),
      child: Container(
        height: 90,
        width: 330,
        padding: const EdgeInsets.symmetric(vertical: 8),
        decoration: BoxDecoration(
          color: const Color(0xFFEFEFEF),
          borderRadius: BorderRadius.circular(24),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceEvenly,
          children: [
            Container(
              width: 74,
              height: 74,
              decoration: BoxDecoration(
                color: const Color(0xFF1F2427),
                borderRadius: BorderRadius.circular(18),
              ),
              child: const Icon(Icons.search, color: Colors.white, size: 34),
            ),
            const Padding(
              padding: EdgeInsets.symmetric(horizontal: 12),
              child: Icon(
                Icons.notifications_none_rounded,
                color: Colors.black87,
                size: 34,
              ),
            ),
            const Padding(
              padding: EdgeInsets.symmetric(horizontal: 12),
              child: Icon(
                Icons.favorite_border_rounded,
                color: Colors.black87,
                size: 34,
              ),
            ),
            const Padding(
              padding: EdgeInsets.symmetric(horizontal: 12),
              child: Icon(
                Icons.calendar_today_rounded,
                color: Colors.black87,
                size: 32,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class Page2 extends StatelessWidget {
  final String name;
  final String location;
  final double rating;
  final double price;
  final String image;
  final int selectedIndex;

  const Page2({
    super.key,
    required this.name,
    required this.location,
    required this.rating,
    required this.price,
    required this.image,
    required this.selectedIndex,
  });

  @override
  Widget build(BuildContext context) {
    final double topPadding = MediaQuery.of(context).padding.top;

    return Scaffold(
      backgroundColor: const Color(0xFFF2F6F9),
      body: Stack(
        children: [
          SingleChildScrollView(
            physics: const BouncingScrollPhysics(),
            padding: const EdgeInsets.only(bottom: 180),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                HotelHeaderCard(
                  topPadding: topPadding,
                  image: image,
                  price: price,
                  selectedIndex: selectedIndex,
                ),
                const SizedBox(height: 14),
                const PageIndicators(),
                const SizedBox(height: 36),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 20),
                  child: HotelTitleSection(
                    name: name,
                    location: location,
                    rating: rating,
                  ),
                ),
                const SizedBox(height: 38),
                const AmenitiesSection(),
                const SizedBox(height: 38),
                const AboutSection(),
                const SizedBox(height: 24),
                GallerySection(image: image),
              ],
            ),
          ),
          Align(
            alignment: Alignment.bottomCenter,
            child: BottomBookingBar(hotelName: name, price: price),
          ),
        ],
      ),
    );
  }
}

class HotelHeaderCard extends StatelessWidget {
  final double topPadding;
  final String image;
  final double price;
  final int selectedIndex;

  const HotelHeaderCard({
    super.key,
    required this.topPadding,
    required this.image,
    required this.price,
    required this.selectedIndex,
  });

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: const BorderRadius.only(
        bottomLeft: Radius.circular(32),
        bottomRight: Radius.circular(32),
      ),
      child: Stack(
        children: [
          SizedBox(
            width: double.infinity,
            height: 320 + topPadding,
            child: Image.asset(image, fit: BoxFit.cover),
          ),
          Positioned(
            top: topPadding + 16,
            left: 20,
            right: 20,
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                _buildCircleButton(
                  icon: Icons.chevron_left,
                  onTap: () => Navigator.pop(context, selectedIndex),
                ),
                _buildCircleButton(
                  icon: Icons.favorite,
                  iconColor: const Color(0xFFFF2D55),
                  onTap: () {},
                ),
              ],
            ),
          ),
          Positioned(
            bottom: 36,
            right: 0,
            child: ClipRRect(
              borderRadius: const BorderRadius.only(
                topLeft: Radius.circular(18),
                bottomLeft: Radius.circular(18),
              ),
              child: BackdropFilter(
                filter: ImageFilter.blur(sigmaX: 16, sigmaY: 16),
                child: Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 20,
                    vertical: 14,
                  ),
                  color: Colors.black.withOpacity(0.35),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Text('🔥', style: TextStyle(fontSize: 18)),
                          const SizedBox(width: 8),
                          Text(
                            '\$${price.toStringAsFixed(2)}/',
                            style: const TextStyle(
                              color: Colors.white,
                              fontSize: 18,
                              fontWeight: FontWeight.bold,
                              height: 1.1,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 2),
                      const Padding(
                        padding: EdgeInsets.only(left: 28),
                        child: Text(
                          'night',
                          style: TextStyle(
                            color: Colors.white70,
                            fontSize: 13,
                            fontWeight: FontWeight.w400,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildCircleButton({
    required IconData icon,
    Color iconColor = Colors.black,
    required VoidCallback onTap,
  }) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        width: 44,
        height: 44,
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(14),
        ),
        child: Icon(icon, color: iconColor, size: 24),
      ),
    );
  }
}

class HotelTitleSection extends StatelessWidget {
  final String name;
  final String location;
  final double rating;

  const HotelTitleSection({
    super.key,
    required this.name,
    required this.location,
    required this.rating,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          name,
          style: const TextStyle(
            fontSize: 26,
            fontWeight: FontWeight.bold,
            color: Colors.black,
            letterSpacing: -0.5,
          ),
        ),
        const SizedBox(height: 12),
        Row(
          children: [
            const Icon(Icons.location_on, size: 20, color: Colors.grey),
            const SizedBox(width: 6),
            Expanded(
              child: Text(
                location,
                style: const TextStyle(color: Colors.grey, fontSize: 15),
              ),
            ),
            Text(
              rating.toString(),
              style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 17),
            ),
            const SizedBox(width: 4),
            const Icon(Icons.star, size: 20, color: Colors.amber),
          ],
        ),
      ],
    );
  }
}

class PageIndicators extends StatelessWidget {
  const PageIndicators({super.key});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        _buildDot(10, Colors.black),
        const SizedBox(width: 19),
        _buildDot(6, Colors.grey),
        const SizedBox(width: 19),
        _buildDot(6, Colors.grey),
        const SizedBox(width: 19),
        _buildDot(6, Colors.grey),
        const SizedBox(width: 19),
        _buildDot(6, Colors.grey),
        const SizedBox(width: 19),
        _buildDot(6, Colors.grey),
      ],
    );
  }

  Widget _buildDot(double size, Color color) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(color: color, shape: BoxShape.circle),
    );
  }
}

class AmenitiesSection extends StatelessWidget {
  const AmenitiesSection({super.key});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Amenities',
            style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 16),
          Row(
            children: const [
              Expanded(
                flex: 3,
                child: AmenityChip(icon: Icons.wifi, label: 'Wi-Fi'),
              ),
              SizedBox(width: 12),
              Expanded(
                flex: 4,
                child: AmenityChip(icon: Icons.air, label: 'Air conditioning'),
              ),
              SizedBox(width: 12),
              Expanded(flex: 2, child: AmenityChip(label: '+3')),
            ],
          ),
        ],
      ),
    );
  }
}

class AmenityChip extends StatelessWidget {
  final IconData? icon;
  final String label;

  const AmenityChip({super.key, this.icon, required this.label});

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 56,
      alignment: Alignment.center,
      padding: const EdgeInsets.symmetric(horizontal: 14),
      decoration: BoxDecoration(
        color: const Color(0xFFEBEFF3),
        borderRadius: BorderRadius.circular(18),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          if (icon != null) ...[
            Icon(icon, size: 20, color: Colors.black87),
            const SizedBox(width: 8),
          ],
          Flexible(
            child: Text(
              label,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w600,
                color: Colors.black87,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class AboutSection extends StatelessWidget {
  const AboutSection({super.key});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: const [
          Text(
            'About',
            style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
          ),
          SizedBox(height: 14),
          Text(
            'This is a perfect place to stay when you\'re at Siargao. Comes with parking lot & every tricycle driver knows the place.',
            style: TextStyle(
              color: Color(0xFF6E7985),
              fontSize: 15.5,
              height: 1.6,
              fontWeight: FontWeight.w400,
            ),
          ),
        ],
      ),
    );
  }
}

class GallerySection extends StatelessWidget {
  final String image;

  const GallerySection({super.key, required this.image});

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 85,
      child: ListView.builder(
        scrollDirection: Axis.horizontal,
        physics: const BouncingScrollPhysics(),
        padding: const EdgeInsets.symmetric(horizontal: 20),
        itemCount: 4,
        itemBuilder: (context, index) {
          return Container(
            width: 100,
            margin: const EdgeInsets.only(right: 12),
            child: ClipRRect(
              borderRadius: BorderRadius.circular(18),
              child: Image.asset(image, fit: BoxFit.cover),
            ),
          );
        },
      ),
    );
  }
}

class BottomBookingBar extends StatelessWidget {
  final String hotelName;
  final double price;

  const BottomBookingBar({
    super.key,
    required this.hotelName,
    required this.price,
  });

  @override
  Widget build(BuildContext context) {
    return ClipRect(
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: 16, sigmaY: 16),
        child: Container(
          width: double.infinity,
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: [
                Colors.white.withOpacity(0.05),
                Colors.white.withOpacity(0.25),
                Colors.white.withOpacity(0.40),
              ],
            ),
            border: Border(
              top: BorderSide(color: Colors.white.withOpacity(0.3), width: 0.8),
            ),
          ),
          child: Padding(
            padding: const EdgeInsets.fromLTRB(20, 16, 20, 16),
            child: SizedBox(
              width: double.infinity,
              height: 56,
              child: ElevatedButton(
                onPressed: () {
                  Navigator.pushReplacement(
                    context,
                    MaterialPageRoute(
                      builder: (context) =>
                          BookingPage(hotelName: hotelName, price: price),
                    ),
                  );
                },
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF1E2022),
                  elevation: 0,
                  splashFactory: NoSplash.splashFactory,
                  shadowColor: Colors.transparent,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(20),
                  ),
                ),
                child: const Text(
                  'Book',
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 16,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}


class BookingPage extends StatelessWidget {
  final String hotelName;
  final double price;

  const BookingPage({super.key, required this.hotelName, required this.price});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF2F6F9),
      appBar: AppBar(
        backgroundColor: const Color(0xFFF2F6F9),
        elevation: 0,
        title: const Text(
          'Booking',
          style: TextStyle(color: Colors.black, fontWeight: FontWeight.bold),
        ),
        iconTheme: const IconThemeData(color: Colors.black),
      ),
      body: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SizedBox(height: 20),
            const Text(
              'Your reservation',
              style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 25),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(24),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Hotel',
                    style: TextStyle(color: Colors.grey, fontSize: 14),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    hotelName,
                    style: const TextStyle(
                      fontSize: 20,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 20),
                  const Text(
                    'Price per night',
                    style: TextStyle(color: Colors.grey, fontSize: 14),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    '\$${price.toStringAsFixed(2)}',
                    style: const TextStyle(
                      fontSize: 20,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
              ),
            ),
            const Spacer(),
            SizedBox(
              width: double.infinity,
              height: 56,
              child: ElevatedButton(
                onPressed: () {
                  Navigator.popUntil(context, (route) => route.isFirst);
                },
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF1E2022),
                  elevation: 0,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(20),
                  ),
                ),
                child: const Text(
                  'Confirm booking and go home',
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 16,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
            ),
            const SizedBox(height: 15),
          ],
        ),
      ),
    );
  }
}

class Page1 extends StatelessWidget {
  const Page1({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF2F6F9),
      appBar: AppBar(
        toolbarHeight: 80,
        backgroundColor: const Color(0xFFF2F6F9),
        title: Row(
          children: [
            Text.rich(
              TextSpan(
                style: const TextStyle(
                  fontSize: 26,
                  fontWeight: FontWeight.bold,
                ),
                children: [
                  const TextSpan(text: "Find "),
                  TextSpan(
                    text: "the best",
                    style: const TextStyle(backgroundColor: Color(0xFF34D6CE)),
                  ),
                  const TextSpan(text: "\nfor your holiday"),
                ],
              ),
            ),
            const Spacer(),
            Padding(
              padding: const EdgeInsets.only(right: 12, top: 15),
              child: Image.asset("assets/search.png", scale: 3, height: 29),
            ),
          ],
        ),
        actions: [
          IconButton(
            tooltip: 'Инструменты устройства',
            icon: const Icon(Icons.devices_other_rounded),
            onPressed: () {
              Navigator.push(
                context,
                MaterialPageRoute<void>(
                  builder: (context) => const DeviceToolsPage(),
                ),
              );
            },
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.only(top: 30),
              child: SizedBox(
                height: 60,
                child: ListView(
                  scrollDirection: Axis.horizontal,
                  padding: const EdgeInsets.only(left: 20),
                  children: const [
                    Categories(text: "All", num: "123", checked: false),
                    Categories(text: "Hotels", num: "24", checked: true),
                    Categories(text: "Apartaments", num: "99", checked: false),
                  ],
                ),
              ),
            ),
            Row(
              children: const [
                Padding(
                  padding: EdgeInsets.only(left: 20, top: 25),
                  child: Text(
                    "Popular hotels",
                    style: TextStyle(fontSize: 21, fontWeight: FontWeight.bold),
                  ),
                ),
                Spacer(),
                Padding(
                  padding: EdgeInsets.only(right: 20, top: 25),
                  child: Text(
                    "See all  〉",
                    style: TextStyle(fontSize: 16, fontWeight: FontWeight.w400),
                  ),
                ),
              ],
            ),
            const Carousel(),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Container(
                  margin: const EdgeInsets.symmetric(horizontal: 4),
                  width: 8,
                  height: 8,
                  decoration: const BoxDecoration(
                    color: Colors.black,
                    shape: BoxShape.circle,
                  ),
                ),
                ...List.generate(
                  5,
                  (index) => Container(
                    margin: const EdgeInsets.symmetric(horizontal: 9),
                    width: 6,
                    height: 6,
                    decoration: BoxDecoration(
                      color: Colors.black.withOpacity(0.2),
                      shape: BoxShape.circle,
                    ),
                  ),
                ),
              ],
            ),
            const BottomMenu(),
          ],
        ),
      ),

      bottomNavigationBar: const WifiSSIDIndicator(),
    );
  }
}

class DeviceToolsPage extends StatefulWidget {
  const DeviceToolsPage({super.key});

  @override
  State<DeviceToolsPage> createState() => _DeviceToolsPageState();
}

class _DeviceToolsPageState extends State<DeviceToolsPage> {
  static const _deviceActions = MethodChannel('device_actions');
  static const _batteryChannel = MethodChannel('battery_channel');

  final _latitudeController = TextEditingController(text: '37.4219983');
  final _longitudeController = TextEditingController(text: '-122.084');
  Uint8List? _photoBytes;
  String _batteryLevel = 'Загрузка...';
  bool _isLoadingBattery = false;

  @override
  void initState() {
    super.initState();
    _loadBatteryLevel();
  }

  @override
  void dispose() {
    _latitudeController.dispose();
    _longitudeController.dispose();
    super.dispose();
  }

  Future<void> _loadBatteryLevel() async {
    if (_isLoadingBattery) return;

    setState(() {
      _isLoadingBattery = true;
    });

    try {
      final level = await _batteryChannel.invokeMethod<int>('getBatteryLevel');
      if (!mounted) return;
      setState(() {
        _batteryLevel = level == null ? 'Недоступно' : '$level%';
      });
    } on PlatformException catch (error) {
      if (!mounted) return;
      setState(() {
        _batteryLevel = error.message ?? 'Ошибка получения заряда';
      });
    } on MissingPluginException {
      if (!mounted) return;
      setState(() {
        _batteryLevel = 'Канал батареи недоступен';
      });
    } finally {
      if (mounted) {
        setState(() {
          _isLoadingBattery = false;
        });
      }
    }
  }

  Future<void> _openMap() async {
    final latitude = double.tryParse(_latitudeController.text.trim());
    final longitude = double.tryParse(_longitudeController.text.trim());

    if (latitude == null ||
        longitude == null ||
        !latitude.isFinite ||
        !longitude.isFinite ||
        latitude < -90 ||
        latitude > 90 ||
        longitude < -180 ||
        longitude > 180) {
      _showMessage('Введите корректные координаты широты и долготы');
      return;
    }

    try {
      await _deviceActions.invokeMethod<void>('openMap', {
        'latitude': latitude,
        'longitude': longitude,
      });
    } on PlatformException catch (error) {
      _showMessage(error.message ?? 'Не удалось открыть карту');
    } on MissingPluginException {
      _showMessage('Канал открытия карт недоступен');
    }
  }

  Future<void> _capturePhoto() async {
    try {
      final photo = await _deviceActions.invokeMethod<Uint8List>(
        'capturePhoto',
      );
      if (!mounted || photo == null) return;
      setState(() {
        _photoBytes = photo;
      });
    } on PlatformException catch (error) {
      _showMessage(error.message ?? 'Не удалось открыть камеру');
    } on MissingPluginException {
      _showMessage('Канал камеры недоступен');
    }
  }

  void _showMessage(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final platformName = switch (Theme.of(context).platform) {
      TargetPlatform.android => 'Android',
      TargetPlatform.iOS => 'iOS',
      _ => 'устройство',
    };

    return Scaffold(
      backgroundColor: const Color(0xFFF2F6F9),
      appBar: AppBar(
        title: const Text('Инструменты устройства'),
        backgroundColor: const Color(0xFFF2F6F9),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text(
                        'Открыть карту',
                        style: Theme.of(context).textTheme.titleLarge,
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _latitudeController,
                        keyboardType: const TextInputType.numberWithOptions(
                          decimal: true,
                          signed: true,
                        ),
                        decoration: const InputDecoration(
                          labelText: 'Широта',
                          border: OutlineInputBorder(),
                        ),
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _longitudeController,
                        keyboardType: const TextInputType.numberWithOptions(
                          decimal: true,
                          signed: true,
                        ),
                        decoration: const InputDecoration(
                          labelText: 'Долгота',
                          border: OutlineInputBorder(),
                        ),
                      ),
                      const SizedBox(height: 12),
                      FilledButton.icon(
                        onPressed: _openMap,
                        icon: const Icon(Icons.map_outlined),
                        label: const Text('Открыть в приложении карт'),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 12),
              Card(
                child: ListTile(
                  leading: const Icon(Icons.battery_charging_full),
                  title: Text('Заряд батареи ($platformName)'),
                  subtitle: Text(_batteryLevel),
                  trailing: IconButton(
                    tooltip: 'Обновить заряд',
                    onPressed: _isLoadingBattery ? null : _loadBatteryLevel,
                    icon: _isLoadingBattery
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.refresh),
                  ),
                ),
              ),
              const SizedBox(height: 12),
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text(
                        'Фотография с камеры',
                        style: Theme.of(context).textTheme.titleLarge,
                      ),
                      const SizedBox(height: 12),
                      FilledButton.icon(
                        onPressed: _capturePhoto,
                        icon: const Icon(Icons.camera_alt_outlined),
                        label: const Text('Сделать фото'),
                      ),
                      if (_photoBytes case final bytes?) ...[
                        const SizedBox(height: 12),
                        ClipRRect(
                          borderRadius: BorderRadius.circular(12),
                          child: Image.memory(
                            bytes,
                            height: 240,
                            fit: BoxFit.cover,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class WifiSSIDIndicator extends StatefulWidget {
  const WifiSSIDIndicator({super.key});

  @override
  State<WifiSSIDIndicator> createState() => _WifiSSIDIndicatorState();
}

class _WifiSSIDIndicatorState extends State<WifiSSIDIndicator>
    with WidgetsBindingObserver {
  static const platform = MethodChannel('wifi_channel');

  String wifiSSID = 'Получение Wi-Fi...';
  bool isLoading = false;

  @override
  void initState() {
    super.initState();

    WidgetsBinding.instance.addObserver(this);
    getWifiSSID();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      getWifiSSID();
    }
  }

  Future<void> getWifiSSID() async {
    if (isLoading) return;

    setState(() {
      isLoading = true;
    });

    try {
      final String result = await platform.invokeMethod('getWifiSSID');

      if (!mounted) return;

      setState(() {
        wifiSSID = result;
      });
    } on PlatformException catch (e) {
      if (!mounted) return;

      setState(() {
        wifiSSID = e.message ?? 'Ошибка получения Wi-Fi';
      });
    } on MissingPluginException {
      if (!mounted) return;

      setState(() {
        wifiSSID = 'Method Channel недоступен';
      });
    } finally {
      if (mounted) {
        setState(() {
          isLoading = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      top: false,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
        decoration: BoxDecoration(
          color: const Color(0xFF202B38),
          borderRadius: BorderRadius.circular(18),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withOpacity(0.12),
              blurRadius: 12,
              offset: const Offset(0, 4),
            ),
          ],
        ),
        child: Row(
          children: [
            const Icon(Icons.wifi, color: Colors.white, size: 24),

            const SizedBox(width: 12),

            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Text(
                    'Подключённая Wi-Fi сеть',
                    style: TextStyle(color: Colors.white70, fontSize: 11),
                  ),

                  const SizedBox(height: 3),

                  Text(
                    wifiSSID,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 15,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ),

            IconButton(
              onPressed: isLoading ? null : getWifiSSID,
              icon: isLoading
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
                        color: Colors.white,
                      ),
                    )
                  : const Icon(Icons.refresh, color: Colors.white),
            ),
          ],
        ),
      ),
    );
  }
}
